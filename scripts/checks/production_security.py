#!/usr/bin/env python3
"""Scan an exact release image without mounting the Docker socket or application data."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile

SCANNER='aquasec/trivy:0.76.1@sha256:790fd66d1697395d8693d1acaa1ea702406a20f109b6fc7bac8db623eca6c06c'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image',required=True)
    parser.add_argument('--directory',type=Path,required=True)
    parser.add_argument('--cache-directory',type=Path)
    args=parser.parse_args()
    root=args.directory
    cache=args.cache_directory or root/'cache'
    for path in [root,cache]:
        if not path.is_absolute() or path.resolve()!=path:
            raise ValueError('absolute_private_scan_directory_required')
        path.mkdir(mode=0o700,parents=True,exist_ok=True)
        if path.stat().st_mode & 0o077: raise ValueError('private_scan_directory_required')
    (cache/'tmp').mkdir(mode=0o700,exist_ok=True)
    image=json.loads(subprocess.check_output(['docker','image','inspect',args.image]))[0]
    if shutil.disk_usage(root).free < image['Size']+4*1024**3:
        raise ValueError('scan_scratch_headroom')
    archive=root/'image.tar'; report=root/'report.json'
    if archive.exists() or report.exists(): raise ValueError('new_scan_report_required')
    try:
        subprocess.run(['docker','image','save','--output',str(archive),image['Id']],check=True)
        with tarfile.open(archive) as contents:
            manifest=json.load(contents.extractfile('manifest.json'))
            if len(manifest)!=1: raise ValueError('single_image_archive_required')
            configuration=contents.extractfile(manifest[0]['Config']).read()
            configuration_id='sha256:'+hashlib.sha256(configuration).hexdigest()
        with (root/'scanner.log').open('w') as log:
            subprocess.run(['docker','run','--rm','--user',str(os.getuid())+':'+str(os.getgid()),
                '--memory','2g','--cpus','2','--pids-limit','128','--read-only','--cap-drop','ALL',
                '--security-opt','no-new-privileges:true','--tmpfs','/tmp:rw,noexec,nosuid,size=64m',
                '-e','TMPDIR=/cache/tmp','-v',str(cache)+':/cache','-v',str(root)+':/scan',SCANNER,
                'image','--quiet','--cache-dir','/cache','--scanners','vuln','--severity','HIGH,CRITICAL',
                '--format','json','--output','/scan/report.json','--input','/scan/image.tar'],
                check=True,stdout=log,stderr=subprocess.STDOUT,timeout=900)
        data=json.loads(report.read_text())
        if data.get('Metadata',{}).get('ImageID')!=configuration_id:
            raise ValueError('scanned_image_identity_mismatch')
        findings=[v for result in data.get('Results',[]) for v in result.get('Vulnerabilities',[])]
        summary={'status':'failed' if findings else 'passed','image':image['Id'],'scanner':SCANNER,
            'critical':sum(v['Severity']=='CRITICAL' for v in findings),
            'high':sum(v['Severity']=='HIGH' for v in findings),
            'advisories':sorted({v['VulnerabilityID'] for v in findings})}
        (root/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
        print(json.dumps(summary)); return 1 if findings else 0
    finally: archive.unlink(missing_ok=True)

if __name__=='__main__': raise SystemExit(main())
