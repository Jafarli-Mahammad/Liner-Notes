#!/usr/bin/env python3
"""Rehearse restoration with disposable loopback projects and report no account payloads."""
import argparse
import hashlib
import http.client
import ipaddress
import json
from pathlib import Path
import secrets
import shutil
import socket
import ssl
import sys
import tempfile
import time

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/ops'))
import production as ops
from init_production import initialize


class Site:
    def __init__(self,operations):
        self.operations=operations
        self.environment=dict(line.split('=',1) for line in (operations.private/'deployment.env').read_text().splitlines() if line)
        if self.environment['LINER_SITE']!='liner.test': raise ops.OperationStopped('disposable_loopback_site_required')
        self.port=int(self.environment.get('LINER_ACCEPTANCE_PORT','8443'))
        self.ca=operations.state/'test-root.crt'
        for _ in range(30):
            try:
                operations.run(['docker','cp',operations.service_id('proxy')+':/data/caddy/pki/authorities/local/root.crt',str(self.ca)])
                break
            except ops.OperationStopped: time.sleep(1)
        else: raise ops.OperationStopped('disposable_test_ca_unavailable')

    def request(self,method,path,body=None,token=None,cookie=None,csrf=None,extra_headers=None):
        port=self.port
        context=ssl.create_default_context(cafile=self.ca)
        class Connection(http.client.HTTPSConnection):
            def connect(self):
                self.sock=self._context.wrap_socket(socket.create_connection(('127.0.0.1',port),timeout=15),server_hostname='liner.test')
        connection=Connection('liner.test',port,context=context,timeout=15)
        headers={'Host':'liner.test:'+str(port),'Origin':'https://liner.test:'+str(port)}
        if token: headers['Authorization']='Bearer '+token
        if cookie: headers['Cookie']=cookie
        if csrf: headers['X-CSRF-TOKEN']=csrf
        if extra_headers: headers.update(extra_headers)
        if body is not None: headers['Content-Type']='application/json'
        connection.request(method,path,json.dumps(body) if body is not None else None,headers)
        response=connection.getresponse(); data=response.read(8*1024*1024); status=response.status; metadata=response.getheaders()
        connection.close()
        try: data=json.loads(data)
        except ValueError: pass
        return status,data,metadata

    def login(self,fixture):
        status,data,_=self.request('POST','/api/auth/login',{'email':fixture['email'],'password':fixture['password']})
        if status!=200: raise ops.OperationStopped('restored_login_failed')
        return data['accessToken']


def verify_account(source,target,fixture):
    a=source.login(fixture); b=target.login(fixture)
    source_signals=source.request('GET','/api/taste/signals',token=a)
    target_signals=target.request('GET','/api/taste/signals',token=b)
    if source_signals[0]!=200 or target_signals[0]!=200: raise ops.OperationStopped('restored_taste_read_failed')
    canonical=lambda signals:sorted(json.dumps(s,sort_keys=True) for s in signals)
    if canonical(source_signals[1])!=canonical(target_signals[1]) or len(target_signals[1])!=3:
        raise ops.OperationStopped('restored_taste_mismatch')
    bootstrap=source.request('GET','/api/discovery/session',token=a)
    cookie='; '.join(v.split(';',1)[0] for k,v in bootstrap[2] if k.lower()=='set-cookie')
    proof=target.request('POST','/api/discovery/session',token=a,cookie=cookie,csrf=bootstrap[1]['csrfToken'])
    if proof[0]!=200: raise ops.OperationStopped('restored_protected_token_rejected')
    if target.request('GET','/api/export/my-data',token=b)[0]!=200:
        raise ops.OperationStopped('restored_export_failed')
    return {'passwordLogin':True,'tasteSignals':3,'protectedTokenAndWrite':True,'export':True}


def table_fingerprints(operations):
    tables=operations.command('exec','-T','db','psql','-X','-A','-t','-U','liner_operator','-d','liner_notes','-c',
        "SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity') AND tablename NOT IN ('ManagedStorageLedger','ManagedStorageCommit','__EFMigrationsHistory') ORDER BY 1,2").decode().splitlines()
    result={}
    for line in tables:
        schema,table=line.split('|'); qualified='"'+schema.replace('"','""')+'"."'+table.replace('"','""')+'"'
        raw=operations.command('exec','-T','db','psql','-X','-A','-t','-U','liner_operator','-d','liner_notes','-c',
            "SELECT encode(sha256(convert_to(to_jsonb(t)::text,'UTF8')),'hex') FROM "+qualified+" t ORDER BY 1")
        result[line]={'rows':len(raw.splitlines()),'sha256':hashlib.sha256(raw).hexdigest()}
    return result


def free_port():
    with socket.socket() as sock: sock.bind(('127.0.0.1',0)); return sock.getsockname()[1]


def available_networks():
    # Docker rejects overlaps; use distinct test ranges and leave all existing networks alone.
    import subprocess
    ids=subprocess.check_output(['docker','network','ls','--quiet'],text=True).split()
    existing=json.loads(subprocess.check_output(['docker','network','inspect',*ids])) if ids else []
    used=[ipaddress.ip_network(c['Subnet']) for n in existing for c in (n.get('IPAM',{}).get('Config') or []) if c.get('Subnet')]
    available=[]
    for i in range(100,220):
        candidate=ipaddress.ip_network(f'172.30.{i}.0/28')
        if not any(candidate.overlaps(x) for x in used if x.version==4): available.append(str(candidate))
        if len(available)==2: break
    return available


def rehearse(images,root):
    networks=available_networks()
    if len(networks)!=2: raise ops.OperationStopped('disposable_networks_unavailable')
    image_env={key:images[service] for service,key in {'web':'LINER_WEB_IMAGE','worker':'LINER_WORKER_IMAGE','migrate':'LINER_TOOLS_IMAGE','volume-init':'LINER_VOLUME_IMAGE'}.items()}
    owned=[]
    try:
        source_path=initialize(root/'source','liner.test',image_env,approved=True,acceptance=True,network=networks[0],port=free_port())
        source=ops.Operations(json.loads(source_path.read_text())); owned.append(source)
        source.command('up','-d','--wait','--wait-timeout','90','db')
        source.command('run','--rm','--no-deps','volume-init')
        source.command('run','--rm','--no-deps','migrate')
        source.worker('establish-storage',operator=True)
        source.command('up','-d','web','proxy')
        site=Site(source)
        for _ in range(30):
            try:
                if site.request('GET','/health')[0]==200: break
            except OSError: pass
            time.sleep(1)
        else: raise ops.OperationStopped('source_not_healthy')
        name='recovery'+secrets.token_hex(6)
        fixture={'userName':name,'email':name+'@example.test','password':secrets.token_urlsafe(24)+'Aa1!'}
        status,data,_=site.request('POST','/api/auth/register',dict(fixture,timeZone='UTC',deliveryDay='Friday',deliveryHourUtc=8,weeklyEmailOptIn=False))
        if status!=201: raise ops.OperationStopped('fixture_registration_failed')
        token=data['accessToken']
        if site.request('POST','/api/taste/seed',{'tags':[{'tag':t,'weight':1} for t in ['ambient','electronic','synthpop']],'artists':[]},token=token)[0]!=200:
            raise ops.OperationStopped('fixture_taste_failed')
        site.request('GET','/api/discovery/session',token=token)
        before=table_fingerprints(source)
        source.restic('init')
        with ops.lifecycle(source.state): snapshot=source.backup()['snapshot']
        target_path=initialize(root/'target','liner.test',image_env,approved=True,acceptance=True,network=networks[1],port=free_port())
        config=json.loads(target_path.read_text()); config['local_repository']=source.config['local_repository'];target_path.write_text(json.dumps(config))
        target=ops.Operations(config); owned.append(target)
        for name in ['restic.env','restic-password']: ops.atomic_copy(source.private/name,target.private/name)
        started=time.monotonic()
        with ops.lifecycle(target.state): target.restore(snapshot); target.resume()
        after=table_fingerprints(target)
        if before!=after: raise ops.OperationStopped('restored_row_fingerprints_mismatch')
        checks=verify_account(site,Site(target),fixture)
        target.command('restart','web')
        for _ in range(30):
            try:
                if Site(target).request('GET','/health')[0]==200: break
            except OSError: pass
            time.sleep(1)
        else: raise ops.OperationStopped('restored_restart_failed')
        with ops.lifecycle(target.state): target.worker('verify-storage')
        checks.update(status='passed',tablesCompared=len(before),restoreSeconds=time.monotonic()-started,
            snapshot=snapshot,offHost=False,backup=json.loads((source.state/'last-backup.json').read_text()))
        return checks
    finally:
        for operation in reversed(owned): operation.command('down','--volumes','--remove-orphans')
        # Restic preserves archive ownership; return only this newly created fixture tree to its caller.
        import os,subprocess
        subprocess.run(['docker','run','--rm','--entrypoint','sh','-v',str(root)+':/cleanup',ops.RESTIC,
            '-c','chown -Rh '+str(os.getuid())+':'+str(os.getgid())+' /cleanup'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--disposable',action='store_true',required=True)
    parser.add_argument('--images',type=Path,help='JSON map: web, worker, migrate, volume-init')
    parser.add_argument('--source',type=Path)
    parser.add_argument('--target',type=Path)
    parser.add_argument('--fixture',type=Path)
    args=parser.parse_args()
    try:
        if args.images:
            with tempfile.TemporaryDirectory(prefix='liner-beta-rehearsal-') as directory:
                result=rehearse(json.loads(args.images.read_text()),Path(directory))
        else:
            if not all([args.source,args.target,args.fixture]): parser.error('use --images or --source/--target/--fixture')
            result=verify_account(Site(ops.Operations(json.loads(args.source.read_text()))),
                Site(ops.Operations(json.loads(args.target.read_text()))),json.loads(args.fixture.read_text()))
        print(json.dumps(result)); return 0
    except (ops.OperationStopped,OSError,ValueError,KeyError) as error:
        print(json.dumps({'status':'failed','reason':str(error) if isinstance(error,ops.OperationStopped) else 'recovery_rehearsal_transport_or_configuration'})); return 2


if __name__=='__main__': raise SystemExit(main())
