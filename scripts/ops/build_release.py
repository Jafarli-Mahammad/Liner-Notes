#!/usr/bin/env python3
"""Build all beta release images from one committed or staged Git tree."""
import argparse
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile

ROOT=Path(__file__).resolve().parents[2]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tree',default='HEAD')
    parser.add_argument('--prefix',default='linernotes:beta')
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    tree=subprocess.check_output(['git','rev-parse','--verify',args.tree+'^{tree}'],cwd=ROOT,text=True).strip()
    images={}
    with tempfile.TemporaryDirectory(prefix='liner-release-') as temporary:
        directory=Path(temporary); archive=directory/'source.tar'; source=directory/'source'; source.mkdir()
        subprocess.run(['git','archive','--format=tar','--output',str(archive),tree],cwd=ROOT,check=True)
        with tarfile.open(archive) as content: content.extractall(source,filter='data')
        for target,key in [('web','WEB'),('worker','WORKER'),('tools','TOOLS'),('volume-init','VOLUME')]:
            tag=args.prefix+'-'+target
            subprocess.run(['docker','build','--target',target,'--build-arg','LINER_SOURCE_TREE='+tree,'-t',tag,str(source)],check=True)
            image=json.loads(subprocess.check_output(['docker','image','inspect',tag]))[0]
            if image['Config']['Labels']['org.opencontainers.image.revision']!=tree:
                raise RuntimeError('release_tree_label_mismatch')
            images['LINER_'+key+'_IMAGE']=image['Id']
        subprocess.run(['python3',str(ROOT/'scripts/checks/production_artifact.py'),'--image',images['LINER_WEB_IMAGE']],check=True)
    args.output.write_text(json.dumps({'sourceTree':tree,'images':images},indent=2)+'\n')

if __name__=='__main__': main()
