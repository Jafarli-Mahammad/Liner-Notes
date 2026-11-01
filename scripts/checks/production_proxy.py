#!/usr/bin/env python3
"""Verify exact release assets and security boundaries through disposable HTTPS."""
import argparse
import json
from pathlib import Path
import tempfile
from production_recovery import Site,ops
from production_artifact import image_root,validate

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configuration',type=Path,required=True)
    parser.add_argument('--image',required=True)
    args=parser.parse_args()
    operation=ops.Operations(json.loads(args.configuration.read_text())); site=Site(operation)
    image=operation.run(['docker','inspect',operation.service_id('web'),'--format','{{.Image}}']).decode().strip()
    if image!=args.image: raise RuntimeError('running_release_image_mismatch')
    with tempfile.TemporaryDirectory(prefix='liner-proxy-assets-') as temporary:
        root=Path(temporary); image_root(image,root)
        paths=['/index.html',*validate(root)]
        for path in paths:
            status,body,_=site.request('GET',path)
            if status!=200 or body!=(root/path.lstrip('/')).read_bytes():
                raise RuntimeError('served_release_asset_mismatch')
    checks=0
    for path in ['/ops/status','/manual-test','/api/dev/test','/swagger/index.html']:
        if site.request('GET',path)[0]!=404: raise RuntimeError('private_route_exposed')
        checks+=1
    if site.request('GET','/health/live')[0]!=200: raise RuntimeError('liveness_failed')
    if site.request('GET','/health/ready')[0]!=200: raise RuntimeError('readiness_failed')
    if site.request('GET','/',extra_headers={'Host':'untrusted.example'})[0] not in [400,404,421]:
        raise RuntimeError('untrusted_host_accepted')
    if site.request('POST','/api/discovery/batches',{'revision':0,'requestKey':'invalid'},extra_headers={'Origin':'https://untrusted.example'})[0]!=401:
        raise RuntimeError('untrusted_mutation_origin_accepted')
    if site.request('GET','/health/live',extra_headers={'X-Forwarded-Proto':'http','X-Forwarded-Host':'untrusted.example'})[0]!=200:
        raise RuntimeError('forged_forwarding_not_replaced')
    print(json.dumps({'status':'passed','assetsCompared':len(paths),'privateRoutes':checks,
        'health':True,'hostAndOrigin':True,'forwarding':True,'image':image}))

if __name__=='__main__': main()
