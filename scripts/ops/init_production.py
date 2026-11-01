#!/usr/bin/env python3
"""Generate private local operator configuration; never provision or start a service."""
import argparse
import json
from pathlib import Path
import re
import secrets
import ipaddress


def initialize(directory,site,images,*,approved=False,acceptance=False,network='172.30.85.0/28',port=8443):
    if not directory.is_absolute() or directory.exists() or directory.absolute()!=directory.resolve():
        raise ValueError('new_absolute_private_directory_required')
    if not re.fullmatch(r'[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?',site) or '..' in site:
        raise ValueError('plain_hostname_required')
    if any(not re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9:./_@-]*',value) for value in images.values()):
        raise ValueError('valid_image_references_required')
    subnet=ipaddress.ip_network(network,strict=True)
    if subnet.version!=4 or subnet.prefixlen!=28 or not subnet.is_private or not 1024<=port<=65535:
        raise ValueError('private_ipv4_subnet_and_unprivileged_acceptance_port_required')
    directory.mkdir(mode=0o700,parents=True)
    private=directory/'private'; private.mkdir(mode=0o700)
    passwords={role:secrets.token_hex(32) for role in ['operator','application']}
    signing=secrets.token_hex(32)
    for role,password in passwords.items():
        p=private/(role+'-password'); p.write_text(password); p.chmod(0o444)
    for role,name in [('operator','operator'),('application','runtime')]:
        user='liner_operator' if role=='operator' else 'liner_app'
        settings={'ConnectionStrings':{'DefaultConnection':'Host=db;Database=liner_notes;Username='+user+';Password='+passwords[role]},
                  'Jwt':{'SecretKey':signing}}
        p=private/(name+'-settings.json'); p.write_text(json.dumps(settings)); p.chmod(0o444)
    # Files are mounted read-only into non-root containers; the 0700 parent restricts host access.
    environment={'LINER_SITE':site,'LINER_PRIVATE_DIR':str(private),'LINER_PROVIDER_USE_APPROVED':str(approved).lower(),
                 'LINER_RUN_JOBS':'false','LINER_FRONTEND_SUBNET':str(subnet),'LINER_PROXY_IP':str(subnet[2]),
                 'LINER_WEB_IP':str(subnet[3]),'LINER_ACCEPTANCE_PORT':str(port),**images}
    p=private/'deployment.env'; p.write_text(''.join(k+'='+v+'\n' for k,v in environment.items())); p.chmod(0o600)
    p=private/'restic.env'; p.write_text('RESTIC_REPOSITORY=/repository\n'); p.chmod(0o600)
    p=private/'restic-password'; p.write_text(secrets.token_urlsafe(48)); p.chmod(0o600)
    root=Path(__file__).resolve().parents[2]
    config={'project':'liner-beta-'+secrets.token_hex(6),'private_dir':str(private),'state_dir':str(directory/'operations'),
            'backup_dir':str(directory/'staging'),'local_repository':str(directory/'repository'),
            'compose_files':[str(root/'deploy/compose.production.yaml')]}
    if acceptance: config['compose_files'].append(str(root/'deploy/compose.acceptance.yaml'))
    p=directory/'operations.json'; p.write_text(json.dumps(config)); p.chmod(0o600)
    return p


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory',type=Path,required=True)
    parser.add_argument('--site',required=True)
    parser.add_argument('--web-image',required=True)
    parser.add_argument('--worker-image',required=True)
    parser.add_argument('--tools-image',required=True)
    parser.add_argument('--volume-image',required=True)
    parser.add_argument('--approve-provider-use',action='store_true')
    parser.add_argument('--acceptance',action='store_true')
    parser.add_argument('--network',default='172.30.85.0/28')
    parser.add_argument('--port',type=int,default=8443)
    args=parser.parse_args()
    try:
        initialize(args.directory,args.site,{'LINER_WEB_IMAGE':args.web_image,'LINER_WORKER_IMAGE':args.worker_image,
            'LINER_TOOLS_IMAGE':args.tools_image,'LINER_VOLUME_IMAGE':args.volume_image},approved=args.approve_provider_use,
            acceptance=args.acceptance,network=args.network,port=args.port)
    except (OSError,ValueError): raise SystemExit('Private setup failed; existing directories are never overwritten.')
    print('Private configuration created. Jobs remain paused; the local repository is not an off-host backup.')
