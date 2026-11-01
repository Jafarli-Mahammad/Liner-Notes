#!/usr/bin/env python3
"""Browser acceptance against an explicitly disposable loopback HTTPS stack."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import socket
import ssl
import subprocess
import sys
from production_recovery import Site, ops

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configuration',type=Path,required=True)
    parser.add_argument('--fixture',type=Path)
    parser.add_argument('--browser',required=True)
    parser.add_argument('--live',action='store_true')
    parser.add_argument('--evidence',type=Path,required=True)
    args=parser.parse_args()
    site=Site(ops.Operations(json.loads(args.configuration.read_text())))
    context=ssl.create_default_context(cafile=site.ca)
    context.minimum_version=ssl.TLSVersion.TLSv1_2
    with socket.create_connection(('127.0.0.1',site.port),timeout=15) as connection:
        with context.wrap_socket(connection,server_hostname='liner.test') as secure:
            certificate=secure.getpeercert(binary_form=True)
    # Authenticate through the fixture CA first, then pin this leaf for Chromium.
    public=subprocess.check_output(['openssl','x509','-inform','der','-pubkey','-noout'],input=certificate)
    der=subprocess.check_output(['openssl','pkey','-pubin','-outform','der'],input=public)
    pin=base64.b64encode(hashlib.sha256(der).digest()).decode()
    args.evidence.mkdir(mode=0o700,parents=True,exist_ok=True)
    environment=dict(os.environ,LINER_HOME_URL='https://liner.test:'+str(site.port),
        LINER_TEST_CA_SPKI=pin,LINER_BROWSER_EXECUTABLE=args.browser,
        LINER_LIVE_EVIDENCE_DIR=str(args.evidence.resolve()))
    if args.live: environment['LINER_LIVE_FRONTEND']='1'
    else:
        if not args.fixture: parser.error('--fixture is required without --live')
        environment.update(LINER_PRODUCTION_ACCEPTANCE='1',LINER_PRODUCTION_FIXTURE=str(args.fixture.resolve()))
    return subprocess.call(['npx','playwright','test','live.spec.ts' if args.live else 'production.spec.ts'],
        cwd=ops.ROOT/'src/Presentation/ClientApp',env=environment)

if __name__=='__main__': sys.exit(main())
