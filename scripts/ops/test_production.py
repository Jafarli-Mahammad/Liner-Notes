import importlib.util
import tempfile
import unittest
import json
from pathlib import Path

spec = importlib.util.spec_from_file_location('production_ops',Path(__file__).with_name('production.py'))
ops = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ops)
setup_spec=importlib.util.spec_from_file_location('production_init',Path(__file__).with_name('init_production.py'))
setup=importlib.util.module_from_spec(setup_spec); setup_spec.loader.exec_module(setup)


class BackupManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root/'database.dump').write_bytes(b'fixture-database')
        (self.root/'state').mkdir()
        (self.root/'state'/'artifact').write_bytes(b'fixture-artifact')

    def test_manifest_validates_complete_set(self):
        ops.write_manifest(self.root,{'sourceImage':'fixture'})
        self.assertEqual('fixture',ops.verify_manifest(self.root)['sourceImage'])

    def test_changed_missing_or_additional_files_fail(self):
        for mode in ['changed','missing','extra']:
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root/'payload').write_bytes(b'original')
                ops.write_manifest(root,{})
                if mode == 'changed': (root/'payload').write_bytes(b'changed')
                if mode == 'missing': (root/'payload').unlink()
                if mode == 'extra': (root/'extra').write_bytes(b'extra')
                with self.assertRaises(ops.OperationStopped): ops.verify_manifest(root)

    def test_symlinks_are_never_followed(self):
        (self.root/'link').symlink_to('/etc/passwd')
        with self.assertRaises(ops.OperationStopped): ops.write_manifest(self.root,{})

    def test_concurrent_lifecycle_operations_fail_without_overwriting_marker(self):
        with ops.lifecycle(self.root):
            with self.assertRaises(ops.OperationStopped):
                with ops.lifecycle(self.root): pass

    def test_existing_nonprivate_state_directory_fails(self):
        self.root.chmod(0o777)
        with self.assertRaises(ops.OperationStopped):
            with ops.lifecycle(self.root): pass

    def test_failed_subprocess_never_exposes_output(self):
        operation=object.__new__(ops.Operations)
        with self.assertRaisesRegex(ops.OperationStopped,'operator_command_failed') as error:
            operation.run(['python3','-c','import sys; print("fixture-private-value"); sys.exit(1)'])
        self.assertNotIn('fixture-private-value',str(error.exception))

    def test_snapshot_root_symlink_is_rejected(self):
        other=self.root/'other'; other.mkdir()
        link=self.root/'linked-root'; link.symlink_to(other,target_is_directory=True)
        with self.assertRaises(ops.OperationStopped): ops.contents(link)

    def test_restore_replaces_readonly_secrets_atomically(self):
        source=self.root/'saved'; source.write_bytes(b'restored-secret-fixture')
        target=self.root/'readonly'; target.write_bytes(b'old-fixture'); target.chmod(0o444)
        ops.atomic_copy(source,target,0o444)
        self.assertEqual(source.read_bytes(),target.read_bytes())
        self.assertEqual(0o444,target.stat().st_mode & 0o777)

    def test_private_setup_is_nonoverwriting_and_keeps_jobs_paused(self):
        target=self.root/'setup'
        images={k:'fixture:local' for k in ['LINER_WEB_IMAGE','LINER_WORKER_IMAGE','LINER_TOOLS_IMAGE','LINER_VOLUME_IMAGE']}
        setup.initialize(target,'music.example.test',images)
        self.assertEqual(0o700,(target/'private').stat().st_mode & 0o777)
        self.assertEqual(0o444,(target/'private/application-password').stat().st_mode & 0o777)
        self.assertEqual(0o600,(target/'private/restic-password').stat().st_mode & 0o777)
        self.assertIn('LINER_RUN_JOBS=false',(target/'private/deployment.env').read_text())
        with self.assertRaises(ValueError): setup.initialize(target,'music.example.test',images)

    def test_status_distinguishes_ready_application_from_local_only_backup(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        data={'components':{c:{'healthy':True,'checkedAt':ops.now()} for c in ['Storage','Database','Disk']}}
        operation.command=lambda *a,**kw:b'HTTP/1.0 200 OK\r\n\r\n'+json.dumps(data).encode()
        ops.atomic_json(self.root/'last-backup.json',{'completedAt':ops.now(),'verified':True,'offHost':False})
        report=operation.status(include_application=True)
        self.assertEqual('degraded',report['status'])
        self.assertEqual(['backup_local_only'],report['reasons'])
        ops.atomic_json(self.root/'last-backup.json',{'completedAt':ops.now(),'verified':True,'offHost':True})
        self.assertEqual('healthy',operation.status(include_application=True)['status'])

    def test_stale_probe_expired_lease_and_paused_jobs_are_visible(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        data={'components':{'Database':{'healthy':True,'checkedAt':'2020-01-01T00:00:00+00:00'}},
              'jobsEnabled':False,'queue':{'oldestExpiredLease':ops.now()}}
        operation.command=lambda *a,**kw:b'HTTP/1.0 200 OK\r\n\r\n'+json.dumps(data).encode()
        reasons=operation.status(include_application=True)['reasons']
        for expected in ['health_probe_stale','job_lease_expired','background_jobs_paused']:
            self.assertIn(expected,reasons)

    def test_retry_removes_only_owned_failed_capture_directories(self):
        operation=object.__new__(ops.Operations); operation.staging=self.root
        failed=self.root/('capture-'+'a'*32+'.partial'); failed.mkdir(); (failed/'payload').write_bytes(b'partial')
        unrelated=self.root/'retained'; unrelated.mkdir()
        link=self.root/('capture-'+'b'*32); link.symlink_to(unrelated,target_is_directory=True)
        operation.clean_failed_captures()
        self.assertFalse(failed.exists()); self.assertTrue(unrelated.is_dir()); self.assertTrue(link.is_symlink())

    def test_incomplete_upgrade_cannot_be_resumed(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        ops.atomic_json(self.root/'maintenance.json',{'action':'upgrade','safe_to_resume':False})
        with self.assertRaisesRegex(ops.OperationStopped,'unsafe_maintenance_requires_recovery'): operation.resume()

    def test_unverified_image_set_is_refused_before_backup(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        operation.run=lambda *a,**kw:json.dumps([{'Config':{'Labels':{'org.opencontainers.image.revision':'unknown'}}}]).encode()
        operation.backup=lambda **kw:self.fail('unverified images must not stop the application')
        with self.assertRaisesRegex(ops.OperationStopped,'release_images_must_share_verified_source_tree'):
            operation.upgrade({s:'fixture:local' for s in ['web','worker','volume-init','migrate']})


    def test_mail_status_requires_recent_timer_run_only_when_enabled(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        data={'components':{c:{'healthy':True,'checkedAt':ops.now()} for c in ['Storage','Database','Disk']},'mail':{}}
        operation.command=lambda *a,**kw:b'HTTP/1.0 200 OK\r\n\r\n'+json.dumps(data).encode()
        self.assertIn('mail_timer_missing_or_stale',operation.status(include_application=True)['reasons'])
        ops.atomic_json(self.root/'last-mail.json',{'at':ops.now(),'status':'passed'})
        self.assertNotIn('mail_timer_missing_or_stale',operation.status(include_application=True)['reasons'])
        operation.status()  # Read-only file status does not require an application response.

    def test_mail_and_reconciliation_refuse_maintenance_before_worker(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        operation.worker=lambda *a,**kw:self.fail('maintenance must block the worker')
        ops.atomic_json(self.root/'maintenance.json',{'action':'backup'})
        with self.assertRaisesRegex(ops.OperationStopped,'mail_blocked_by_maintenance'): operation.mail()
        with self.assertRaisesRegex(ops.OperationStopped,'mail_blocked_by_maintenance'):
            operation.reconcile_mail('37e4414c-5e25-4dbc-a071-43552a4bd53b')

    def test_mail_worker_success_statuses_are_recorded_by_real_result_parser(self):
        operation=object.__new__(ops.Operations); operation.state=self.root
        operation.private=self.root/'private'; operation.private.mkdir()
        operation.command=lambda *a,**kw:json.dumps({'Status':'completed','Schedule':{},'Delivery':{}}).encode()
        mail=operation.mail()
        self.assertEqual('completed',mail['Status'])
        self.assertEqual('passed',json.loads((self.root/'last-mail.json').read_text())['status'])
        operation.command=lambda *a,**kw:json.dumps({'Status':'reconciled'}).encode()
        self.assertEqual('reconciled',operation.reconcile_mail('37e4414c-5e25-4dbc-a071-43552a4bd53b')['Status'])

if __name__ == '__main__': unittest.main()
