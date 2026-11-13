#!/usr/bin/env python3
"""Private, stopped-writer operations for the supported single-host Compose deployment."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
RESTIC = 'restic/restic:0.19.1@sha256:136600b6ff6843d61d355f7f71f460a166429f35de6fd11b568fece3c9a4d510'


class OperationStopped(RuntimeError):
    pass


def now():
    return datetime.now(timezone.utc).isoformat()


def atomic_json(path, value):
    path = Path(path)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temporary.open('x') as output:
            os.chmod(temporary,0o600)
            json.dump(value,output,sort_keys=True)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary,path)
        descriptor = os.open(path.parent,os.O_DIRECTORY)
        try: os.fsync(descriptor)
        finally: os.close(descriptor)
    finally:
        temporary.unlink(missing_ok=True)


def private_directory(path):
    path = Path(path)
    if not path.is_absolute() or path.is_symlink() or path.absolute() != path.resolve():
        raise OperationStopped('unsafe_operator_directory')
    path.mkdir(mode=0o700,parents=True,exist_ok=True)
    if path.stat().st_mode & 0o077:
        raise OperationStopped('operator_directory_not_private')
    return path


def atomic_copy(source,destination,mode=0o600):
    temporary=destination.with_name(destination.name+'.'+uuid.uuid4().hex+'.tmp')
    try:
        with source.open('rb') as incoming,temporary.open('xb') as outgoing:
            temporary.chmod(mode)
            shutil.copyfileobj(incoming,outgoing,1024*1024)
            outgoing.flush(); os.fsync(outgoing.fileno())
        os.replace(temporary,destination)
        descriptor=os.open(destination.parent,os.O_DIRECTORY)
        try: os.fsync(descriptor)
        finally: os.close(descriptor)
    finally: temporary.unlink(missing_ok=True)


@contextmanager
def lifecycle(directory):
    directory = private_directory(directory)
    with (directory/'lifecycle.lock').open('a') as lock:
        os.chmod(directory/'lifecycle.lock',0o600)
        try: fcntl.flock(lock,fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError: raise OperationStopped('operation_already_running') from None
        try: yield
        finally: fcntl.flock(lock,fcntl.LOCK_UN)


def file_hash(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda:source.read(1024*1024),b''): digest.update(block)
    return digest.hexdigest()


def contents(root):
    if root.is_symlink(): raise OperationStopped('snapshot_symlink_forbidden')
    result = {}
    for path in sorted(root.rglob('*')):
        if path.is_symlink(): raise OperationStopped('snapshot_symlink_forbidden')
        if path.is_dir(): continue
        if not stat.S_ISREG(path.stat().st_mode): raise OperationStopped('snapshot_nonregular_file')
        name = path.relative_to(root).as_posix()
        if name == 'manifest.json': continue
        result[name] = {'sha256':file_hash(path),'bytes':path.stat().st_size}
    return result


def write_manifest(root, metadata):
    root = Path(root)
    atomic_json(root/'manifest.json',dict(metadata,version=1,files=contents(root)))


def verify_manifest(root):
    root = Path(root)
    manifest = root/'manifest.json'
    if manifest.is_symlink() or not manifest.is_file() or manifest.stat().st_size > 16*1024*1024:
        raise OperationStopped('snapshot_manifest_invalid')
    data = json.loads(manifest.read_text())
    if not isinstance(data,dict) or data.get('version') != 1 or data.get('files') != contents(root):
        raise OperationStopped('snapshot_manifest_mismatch')
    return data


class Operations:
    def __init__(self, config):
        self.config = config
        if not re.fullmatch(r'liner-[a-z0-9][a-z0-9-]{0,50}',config['project']):
            raise OperationStopped('explicit_liner_project_required')
        self.state = private_directory(config['state_dir'])
        self.staging = private_directory(config['backup_dir'])
        self.private = private_directory(config['private_dir'])
        if self.staging == self.private or self.staging.is_relative_to(self.private) or self.private.is_relative_to(self.staging):
            raise OperationStopped('backup_staging_overlaps_secrets')
        self.compose = ['docker','compose','--project-name',config['project'],'--env-file',str(self.private/'deployment.env')]
        for name in config.get('compose_files',[str(ROOT/'deploy/compose.production.yaml')]):
            if not Path(name).is_absolute(): raise OperationStopped('absolute_compose_path_required')
            self.compose += ['-f',name]
        self.compose += ['--profile','operator']

    def run(self, args, *, output=None, source=None, timeout=900,allowed_codes=(0,)):
        try:
            result = subprocess.run(args,stdin=source,stdout=output or subprocess.PIPE,stderr=subprocess.PIPE,
                                    timeout=timeout,check=False,env={k:v for k,v in os.environ.items() if not k.startswith('LINER_')})
        except (OSError,subprocess.TimeoutExpired):
            raise OperationStopped('operator_command_unavailable_or_timed_out') from None
        if result.returncode not in allowed_codes: raise OperationStopped('operator_command_failed')
        return result.stdout or b''

    def command(self,*args,**kwargs):
        services = {'web','worker','db','migrate','proxy','volume-init'}
        self.last_step = 'compose_' + args[0] + '_' + next((a for a in args if a in services),'operation')
        return self.run(self.compose+list(args),**kwargs)

    def marker(self,action,phase,**extra):
        atomic_json(self.state/'maintenance.json',dict(action=action,phase=phase,at=now(),**extra))

    def status(self,include_application=False):
        result = {}
        for filename in ['maintenance.json','last-backup.json','last-attempt.json','last-mail.json']:
            path = self.state/filename
            result[filename.removesuffix('.json')] = json.loads(path.read_text()) if path.exists() else None
        if include_application:
            try:
                raw=self.command('exec','-T','web','bash','--noprofile','--norc','-c',
                    'exec 3<>/dev/tcp/127.0.0.1/8080; printf \'GET /ops/status HTTP/1.0\\r\\nHost: %s\\r\\n\\r\\n\' "$AllowedHosts" >&3; cat <&3',timeout=10)
                headers,body=raw.split(b'\r\n\r\n',1)
                if b' 200 ' not in headers.splitlines()[0]: raise ValueError()
                result['application']=json.loads(body)
            except (OperationStopped,ValueError): result['application']={'unavailable':True}
            reasons=[]
            if result['maintenance']: reasons.append('maintenance_active')
            components=result['application'].get('components',{})
            if any(not components.get(c,{}).get('healthy') for c in ['Storage','Disk','Database']):
                reasons.append('application_not_ready')
            if any(not c.get('healthy') for c in components.values()): reasons.append('background_degraded')
            current=datetime.now(timezone.utc)
            if any(not c.get('checkedAt') or (current-datetime.fromisoformat(c['checkedAt'])).total_seconds()>120
                   for name,c in components.items() if name in ['Database','Disk']):
                reasons.append('health_probe_stale')
            if any(c.get('lastFailure') and
                   (current-datetime.fromisoformat(c['lastFailure'])).total_seconds()<900
                   for c in components.values()):
                reasons.append('recent_background_failure')
            queue=result['application'].get('queue') or {}
            if queue.get('oldestExpiredLease'): reasons.append('job_lease_expired')
            if queue.get('oldestCreatedAt') and (current-datetime.fromisoformat(queue['oldestCreatedAt'])).total_seconds()>900:
                reasons.append('job_queue_aged')
            if result['application'].get('jobsEnabled') is False: reasons.append('background_jobs_paused')
            mail=(result.get('application') or {}).get('mail')
            if mail is not None:
                last_mail=result.get('last-mail')
                if not last_mail or (datetime.now(timezone.utc)-datetime.fromisoformat(last_mail['at'])).total_seconds()>900:
                    reasons.append('mail_timer_missing_or_stale')
            backup=result['last-backup']
            if not backup or not backup.get('verified'): reasons.append('verified_backup_missing')
            elif (datetime.now(timezone.utc)-datetime.fromisoformat(backup['completedAt'])).total_seconds()>86400:
                reasons.append('backup_stale')
            if backup and not backup.get('offHost'): reasons.append('backup_local_only')
            if result['last-attempt'] and result['last-attempt'].get('status')=='failed': reasons.append('operator_attempt_failed')
            result.update(status='degraded' if reasons else 'healthy',reasons=reasons)
        return result

    def worker(self,action,*,operator=False,extra=(),mounts=(),configuration_override=None):
        args = ['run','--rm','--no-deps']
        if operator:
            args += ['-e','Production__ConfigurationFile=/run/secrets/operator-settings.json',
                     '-v',str(configuration_override or self.private/'operator-settings.json')+':/run/secrets/operator-settings.json:ro']
        for source,target in mounts: args += ['-v',str(source)+':'+target+':ro']
        acknowledgement=[] if action=='generate' else ['--writers-stopped','true']
        raw = self.command(*args,'worker','--action',action,*acknowledgement,*extra,allowed_codes=(0,2))
        try: result = json.loads(raw)
        except (ValueError,UnicodeDecodeError): raise OperationStopped('operator_result_invalid') from None
        accepted_statuses = {
            'production-mail': {'completed'},
            'reconcile-mail': {'reconciled'},
        }
        if result.get('Status') not in {
            'verified','established','generated','existing','schema_verified','migration_status',
            *accepted_statuses.get(action, set()),
        }:
            reason=result.get('Reason','')
            raise OperationStopped(reason if re.fullmatch('[a-z_]{1,64}',reason) else 'storage_operator_refused')
        return result

    def canonical_schema(self,dump):
        database='liner_schema_'+uuid.uuid4().hex
        sql=['exec','-T','db','psql','-X','-A','-t','-U','liner_operator','-d','liner_notes','-v','ON_ERROR_STOP=1','-c']
        self.command(*sql,'CREATE DATABASE '+database)
        configuration=dump.parent/'schema-operator.json'
        try:
            with dump.open('rb') as source:
                self.command('exec','-T','db','pg_restore','-U','liner_operator','-d',database,
                             '--schema-only','--exit-on-error','--no-owner','--clean','--if-exists',source=source)
            settings=json.loads((self.private/'operator-settings.json').read_text())
            connection=settings['ConnectionStrings']['DefaultConnection']
            if 'Database=liner_notes;' not in connection: raise OperationStopped('canonical_schema_connection_contract')
            settings['ConnectionStrings']['DefaultConnection']=connection.replace('Database=liner_notes;','Database='+database+';',1)
            atomic_json(configuration,settings); configuration.chmod(0o444)
            return self.worker('schema-fingerprint',operator=True,configuration_override=configuration)['SchemaFingerprint']
        finally:
            configuration.unlink(missing_ok=True)
            self.command(*sql,'DROP DATABASE '+database)

    def service_id(self,name):
        ids = self.command('ps','--all','--quiet',name).decode().split()
        if len(ids) != 1: raise OperationStopped('single_service_container_required')
        return ids[0]

    def stop_writers(self,action):
        if (self.state/'maintenance.json').exists(): raise OperationStopped('maintenance_requires_explicit_resume')
        running = self.command('ps','--status','running','--services').decode().split()
        if 'worker' in running: raise OperationStopped('worker_in_progress')
        self.marker(action,'stopping',resume= [s for s in running if s == 'web'],safe_to_resume=action=='backup')
        self.command('stop','--timeout','90','web','worker')
        self.marker(action,'writers_stopped',resume=[s for s in running if s=='web'],safe_to_resume=action=='backup')
        count = self.command('exec','-T','db','psql','-X','-At','-U','liner_operator','-d','liner_notes','-c',
            "SELECT count(*) FROM pg_stat_activity WHERE usename='liner_app'").strip()
        if count != b'0': raise OperationStopped('unmanaged_application_writer_active')

    def resume(self):
        marker = self.status()['maintenance']
        if not marker: return
        if not marker.get('safe_to_resume'): raise OperationStopped('unsafe_maintenance_requires_recovery')
        for service in marker.get('resume',[]): self.command('start',service)
        (self.state/'maintenance.json').unlink()

    def restic(self,*args,snapshot=None,restore=None):
        self.last_step = 'repository_' + args[0]
        env_file = self.private/'restic.env'
        password = self.private/'restic-password'
        if not env_file.is_file() or not password.is_file() or env_file.is_symlink() or password.is_symlink():
            raise OperationStopped('independent_backup_credentials_required')
        command = ['docker','run','--rm','--memory','384m','--cpus','0.5','--pids-limit','128',
                   '--security-opt','no-new-privileges:true','--read-only',
                   '--tmpfs','/root/.cache:rw,noexec,nosuid,size=64m','--tmpfs','/tmp:rw,noexec,nosuid,size=128m',
                   '--env-file',str(env_file),
                   '-e','RESTIC_PASSWORD_FILE=/run/secrets/restic-password',
                   '-v',str(password)+':/run/secrets/restic-password:ro']
        repository = self.config.get('local_repository')
        if repository:
            repository = private_directory(repository)
            if repository == self.staging or repository.is_relative_to(self.staging) or self.staging.is_relative_to(repository):
                raise OperationStopped('repository_overlaps_backup_staging')
            command += ['-v',str(repository)+':/repository','-e','RESTIC_REPOSITORY=/repository']
        if snapshot: command += ['-v',str(snapshot)+':/snapshot:ro']
        if restore: command += ['-v',str(restore)+':/restore']
        return self.run(command+[RESTIC,*args],timeout=3600)

    def clean_failed_captures(self):
        # The host lifecycle lock excludes another capture. Keep the status record,
        # not an unbounded sequence of private, unverified payloads.
        for path in self.staging.iterdir():
            if re.fullmatch(r'capture-[0-9a-f]{32}(\.partial)?',path.name) and path.is_dir() and not path.is_symlink():
                shutil.rmtree(path)

    def backup(self,resume_after_capture=True):
        started=time.monotonic()
        self.stop_writers('backup')
        verification = self.worker('verify-storage')
        self.clean_failed_captures()
        work = self.staging/('capture-'+uuid.uuid4().hex+'.partial')
        work.mkdir(mode=0o700)
        # Reserve scratch before creating any dump/image archive. It is separate from managed quota.
        config = json.loads(self.command('config','--format','json'))
        image_names = sorted({config['services'][s]['image'] for s in ['web','worker','volume-init','migrate']})
        images = json.loads(self.run(['docker','image','inspect',*image_names]))
        image_ids = sorted({i['Id'] for i in images})
        measured = self.command('run','--rm','--no-deps','--entrypoint','du','volume-init','-sb','/app/local-state','/app/keys')
        filesystem_bytes = sum(int(line.split()[0]) for line in measured.splitlines())
        required = verification['Inventory']['DatabaseBytes']*2 + sum(i['Size'] for i in images) + filesystem_bytes
        if required > self.config.get('max_backup_bytes',8*1024**3) or shutil.disk_usage(self.staging).free < required+2*1024**3:
            raise OperationStopped('backup_scratch_headroom')
        with (work/'database.dump').open('xb') as output:
            self.command('exec','-T','db','pg_dump','-U','liner_operator','-d','liner_notes','--format=custom',output=output)
        with (work/'database.dump').open('rb') as source:
            self.command('exec','-T','db','pg_restore','--file=/dev/null',source=source)
        canonical_schema=self.canonical_schema(work/'database.dump')
        web = self.service_id('web')
        for source,name in [('/app/local-state','state'),('/app/keys','keys')]:
            (work/name).mkdir(mode=0o700)
            self.run(['docker','cp',web+':'+source+'/.',str(work/name)])
        secret_dir=work/'private'; secret_dir.mkdir(mode=0o700)
        for name in ['runtime-settings.json','operator-settings.json','operator-password','application-password','deployment.env']:
            shutil.copyfile(self.private/name,secret_dir/name)
            (secret_dir/name).chmod(0o600)
        atomic_json(work/'expected-inventory.json',{'Inventory':verification['Inventory'],'SchemaFingerprint':canonical_schema,
            'SourceSchemaFingerprint':verification['SchemaFingerprint']})
        with (work/'images.tar').open('xb') as output: self.run(['docker','image','save',*image_ids],output=output)
        image_map={service:json.loads(self.run(['docker','image','inspect',config['services'][service]['image']]))[0]['Id']
                   for service in ['web','worker','volume-init','migrate']}
        active_image=self.run(['docker','inspect',web,'--format','{{.Image}}']).decode().strip()
        if active_image != image_map['web']: raise OperationStopped('running_image_differs_from_release_configuration')
        write_manifest(work,{'createdAt':now(),'sourceImages':image_map,'project':self.config['project']})
        verify_manifest(work)
        complete=work.with_suffix(''); os.replace(work,complete)
        if resume_after_capture: self.resume()
        capture_seconds=time.monotonic()-started
        # Initialize a repository explicitly using init-repository; never silently replace a bad repository.
        result=self.restic('backup','--json','/snapshot',snapshot=complete)
        summaries=[json.loads(line) for line in result.splitlines() if line]
        snapshot=next((s['snapshot_id'] for s in summaries if s.get('message_type')=='summary' and 'snapshot_id' in s),None)
        if not snapshot: raise OperationStopped('backup_snapshot_not_published')
        self.restic('check','--read-data')
        atomic_json(self.state/'last-backup.json',{'completedAt':now(),'snapshot':snapshot,'verified':True,
            'offHost':not bool(self.config.get('local_repository')),'captureSeconds':capture_seconds,
            'totalSeconds':time.monotonic()-started})
        self.restic('forget','--keep-daily','7','--keep-weekly','4','--keep-monthly','3','--prune')
        shutil.rmtree(complete)
        atomic_json(self.state/'last-attempt.json',{'at':now(),'action':'backup','status':'passed'})
        return {'status':'verified','snapshot':snapshot,'offHost':not bool(self.config.get('local_repository'))}

    def upgrade(self,images):
        maintenance_existed=(self.state/'maintenance.json').exists()
        try:
            return self._upgrade(images)
        except (OperationStopped,OSError,ValueError,KeyError,TypeError,AttributeError):
            # This method runs only after lifecycle() has acquired its lock.
            if not maintenance_existed and (self.state/'maintenance.json').exists():
                try: self.command('stop','--timeout','90','web')
                except OperationStopped: pass
            raise

    def _upgrade(self,images):
        variables={'web':'LINER_WEB_IMAGE','worker':'LINER_WORKER_IMAGE','migrate':'LINER_TOOLS_IMAGE','volume-init':'LINER_VOLUME_IMAGE'}
        if set(images)!=set(variables) or any(not re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9:./_@-]*',v) for v in images.values()):
            raise OperationStopped('complete_verified_image_set_required')
        inspected={s:json.loads(self.run(['docker','image','inspect',image]))[0] for s,image in images.items()}
        trees={i.get('Config',{}).get('Labels',{}).get('org.opencontainers.image.revision','') for i in inspected.values()}
        if len(trees)!=1 or not re.fullmatch('[0-9a-f]{40}',next(iter(trees))):
            raise OperationStopped('release_images_must_share_verified_source_tree')
        checkpoint=self.backup(resume_after_capture=False)
        prior=self.status()['maintenance']
        self.marker('upgrade','applying',safe_to_resume=False,resume=prior.get('resume',[]),snapshot=checkpoint['snapshot'])
        environment=dict(line.split('=',1) for line in (self.private/'deployment.env').read_text().splitlines() if line and not line.startswith('#'))
        for service,key in variables.items(): environment[key]=inspected[service]['Id']
        temporary=self.state/'upgrade.env'; temporary.write_text(''.join(k+'='+v+'\n' for k,v in environment.items())); temporary.chmod(0o600)
        atomic_copy(self.private/'deployment.env',self.state/'upgrade.before.env')
        atomic_copy(temporary,self.private/'deployment.env'); temporary.unlink()
        # Beta upgrades do not guess how to re-establish trust across an unreviewed schema change.
        if self.worker('migration-status',operator=True)['PendingMigrations']:
            raise OperationStopped('upgrade_schema_change_requires_reviewed_migration')
        self.command('run','--rm','--no-deps','migrate')
        self.worker('verify-storage',operator=True)
        self.command('up','-d','web','proxy')
        for _ in range(60):
            status=self.status(include_application=True)
            components=status.get('application',{}).get('components',{})
            if all(components.get(c,{}).get('healthy') for c in ['Storage','Disk','Database']): break
            time.sleep(1)
        else: raise OperationStopped('upgraded_application_not_ready')
        (self.state/'maintenance.json').unlink()
        atomic_json(self.state/'last-attempt.json',{'at':now(),'action':'upgrade','status':'passed'})
        return {'status':'upgraded_verified','sourceTree':next(iter(trees))}

    def mail(self):
        if (self.state/'maintenance.json').exists():
            raise OperationStopped('mail_blocked_by_maintenance')
        result=self.worker('production-mail')
        atomic_json(self.state/'last-mail.json',{'at':now(),'status':'passed','result':result})
        return result

    def reconcile_mail(self,message_id):
        try:
            if str(uuid.UUID(message_id))!=message_id: raise ValueError()
        except (ValueError,AttributeError): raise OperationStopped('valid_message_id_required') from None
        if (self.state/'maintenance.json').exists(): raise OperationStopped('mail_blocked_by_maintenance')
        return self.worker('reconcile-mail',extra=('--message-id',message_id))

    def generate(self,user,week):
        try:
            parsed=uuid.UUID(user)
            if str(parsed)!=user or parsed.int==0 or not re.fullmatch(r'[0-9]{4}-W[0-9]{2}',week): raise ValueError()
        except (ValueError,AttributeError): raise OperationStopped('explicit_user_and_week_required') from None
        if (self.state/'maintenance.json').exists(): raise OperationStopped('generation_blocked_by_maintenance')
        result=self.worker('generate',extra=('--user',user,'--week',week,'--origin','Live'))
        atomic_json(self.state/'last-attempt.json',{'at':now(),'action':'generate','status':'passed'})
        return result

    def restore(self,snapshot):
        started=time.monotonic()
        if not re.fullmatch('[0-9a-f]{8,64}',snapshot): raise OperationStopped('explicit_snapshot_id_required')
        if self.command('ps','--all','--quiet').strip(): raise OperationStopped('restore_target_not_empty')
        volumes=self.run(['docker','volume','ls','--quiet','--filter','label=com.docker.compose.project='+self.config['project']])
        if volumes.strip(): raise OperationStopped('restore_target_volumes_not_empty')
        if (self.state/'maintenance.json').exists(): raise OperationStopped('restore_already_in_progress')
        self.marker('restore','restoring',safe_to_resume=False)
        # Bound retained failed restores by age and count. The lifecycle lock
        # ensures no other restore is currently using one of these directories.
        cutoff=time.time()-7*24*60*60
        old=sorted((p for p in self.staging.glob('restore-*') if p.is_dir() and not p.is_symlink()),key=lambda p:p.stat().st_mtime)
        for path in old:
            if path.stat().st_mtime < cutoff: shutil.rmtree(path)
        retained=sorted((p for p in self.staging.glob('restore-*') if p.is_dir() and not p.is_symlink()),key=lambda p:p.stat().st_mtime)
        for path in retained[:-4]: shutil.rmtree(path)
        work=self.staging/('restore-'+uuid.uuid4().hex); work.mkdir(mode=0o700)
        self.restic('restore',snapshot,'--target','/restore',restore=work)
        # The restic container restores source numeric ownership. Give the
        # extracted tree to this operator before reading the manifest/files.
        for path in (work,*work.rglob('*')):
            os.chown(path,os.getuid(),os.getgid(),follow_symlinks=False)
        restored=work/'snapshot'
        manifest=verify_manifest(restored)
        with (restored/'images.tar').open('rb') as source: self.run(['docker','image','load'],source=source)
        variables={'web':'LINER_WEB_IMAGE','worker':'LINER_WORKER_IMAGE','migrate':'LINER_TOOLS_IMAGE','volume-init':'LINER_VOLUME_IMAGE'}
        source_images=manifest['sourceImages']
        if set(source_images) != set(variables) or any(not re.fullmatch(r'sha256:[0-9a-f]{64}',v) for v in source_images.values()):
            raise OperationStopped('snapshot_image_identity_invalid')
        environment=dict(line.split('=',1) for line in (self.private/'deployment.env').read_text().splitlines() if line and not line.startswith('#'))
        environment.update({variables[s]:source_images[s] for s in variables})
        (self.private/'deployment.env').write_text(''.join(k+'='+v+'\n' for k,v in environment.items()))
        (self.private/'deployment.env').chmod(0o600)
        # Operator settings are supplied for the target; never overwrite independent backup credentials.
        for name in ['runtime-settings.json','operator-settings.json','operator-password','application-password']:
            atomic_copy(restored/'private'/name,self.private/name,0o444)
        self.command('up','-d','--wait','--wait-timeout','90','db')
        self.command('run','--rm','--no-deps','volume-init')
        with (restored/'database.dump').open('rb') as source:
            self.command('exec','-T','db','pg_restore','-U','liner_operator','-d','liner_notes','--exit-on-error','--no-owner','--clean','--if-exists',source=source)
        # A stopped web container provides the same volume paths as the verified source image.
        self.command('create','web')
        web=self.service_id('web')
        for name,destination in [('state','/app/local-state'),('keys','/app/keys')]:
            self.run(['docker','cp',str(restored/name)+'/.',web+':'+destination])
        uid=self.run(['docker','image','inspect',source_images['web'],'--format','{{.Config.User}}']).decode().strip()
        if not re.fullmatch(r'[0-9]+',uid): raise OperationStopped('numeric_runtime_identity_required')
        self.command('run','--rm','--no-deps','--entrypoint','sh','volume-init','-c',
                     'chown -R '+uid+':'+uid+' /app/local-state /app/keys')
        expected=restored/'expected-inventory.json'; expected.chmod(0o444)
        self.worker('restore-storage',operator=True,extra=('--StorageOperator:ExpectedInventoryFile','/run/secrets/expected-inventory.json'),
                    mounts=[(expected,'/run/secrets/expected-inventory.json')])
        self.command('create','proxy')
        self.marker('restore','verified',safe_to_resume=True,resume=['web','proxy'])
        atomic_json(self.state/'last-backup.json',{'completedAt':manifest['createdAt'],'snapshot':snapshot,'verified':True,
            'offHost':not bool(self.config.get('local_repository'))})
        atomic_json(self.state/'last-attempt.json',{'at':now(),'action':'restore','status':'passed','seconds':time.monotonic()-started})
        shutil.rmtree(work)
        return {'status':'restored_verified','sourceImages':manifest['sourceImages']}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configuration',type=Path,required=True)
    parser.add_argument('action',choices=['backup','restore','upgrade','generate','mail','reconcile-mail','status','resume','init-repository'])
    parser.add_argument('--snapshot')
    parser.add_argument('--images',type=Path,help='Private JSON map: web, worker, migrate, volume-init')
    parser.add_argument('--user')
    parser.add_argument('--week')
    parser.add_argument('--message-id')
    args=parser.parse_args()
    try:
        if not args.configuration.is_absolute() or args.configuration.is_symlink() or args.configuration.stat().st_mode & 0o077:
            raise OperationStopped('operator_configuration_not_private')
        operations=Operations(json.loads(args.configuration.read_text()))
        if args.action=='status':
            result=operations.status(include_application=True)
            print(json.dumps(result)); return 0 if result['status']=='healthy' else 2
        with lifecycle(operations.state):
            if args.action=='backup': result=operations.backup()
            elif args.action=='restore': result=operations.restore(args.snapshot or '')
            elif args.action=='upgrade':
                if not args.images or args.images.is_symlink() or args.images.stat().st_mode & 0o077:
                    raise OperationStopped('private_release_image_map_required')
                result=operations.upgrade(json.loads(args.images.read_text()))
            elif args.action=='generate': result=operations.generate(args.user or '',args.week or '')
            elif args.action=='mail': result=operations.mail()
            elif args.action=='reconcile-mail': result=operations.reconcile_mail(args.message_id or '')
            elif args.action=='resume': operations.resume(); result={'status':'resumed'}
            else: operations.restic('init'); result={'status':'repository_initialized'}
        print(json.dumps(result)); return 0
    except (OperationStopped,OSError,ValueError,KeyError,TypeError,AttributeError) as error:
        reason=str(error) if isinstance(error,OperationStopped) else 'operator_configuration_or_io'
        if 'operations' in locals():
            atomic_json(operations.state/'last-attempt.json',{'at':now(),'action':args.action,'status':'failed','reason':reason,
                'step':getattr(operations,'last_step','configuration')})
        print(json.dumps({'status':'stopped','reason':reason})); return 2


if __name__=='__main__': raise SystemExit(main())
