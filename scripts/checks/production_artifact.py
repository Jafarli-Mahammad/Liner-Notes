#!/usr/bin/env python3
"""Validate release assets; optionally build an image from a Git tree only."""
import argparse
from hashlib import sha256
from html.parser import HTMLParser
import json
import re
from pathlib import Path
import subprocess
import tarfile
import tempfile
from urllib.parse import unquote, urlsplit
from urllib.request import urlopen
import uuid

ROOT = Path(__file__).resolve().parents[2]
LICENSES = ('Inter-OFL.txt', 'DM-Sans-OFL.txt', 'ThirdParty.txt')


class References(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'script' and attrs.get('src'):
            self.assets.append(attrs['src'])
        if tag == 'link' and attrs.get('rel') == 'stylesheet' and attrs.get('href'):
            self.assets.append(attrs['href'])


def validate(root):
    root = Path(root).resolve()
    parser = References()
    parser.feed((root / 'index.html').read_text())
    if not any(urlsplit(p).path.endswith('.js') for p in parser.assets) or not any(
            urlsplit(p).path.endswith('.css') for p in parser.assets):
        raise ValueError('release_requires_script_and_stylesheet')
    pending=list(dict.fromkeys(parser.assets))
    checked=[]
    while pending:
        reference=pending.pop()
        if reference in checked: continue
        checked.append(reference)
        url = urlsplit(reference)
        path = unquote(url.path)
        if url.scheme or url.netloc or not path.startswith('/home-assets/') or '..' in path.split('/'):
            raise ValueError('unsafe_asset_reference')
        target = root / path.lstrip('/')
        if not target.resolve().is_relative_to(root) or not target.is_file() or not target.stat().st_size:
            raise ValueError('missing_release_asset')
        if target.read_bytes().lstrip().lower().startswith((b'<!doctype html', b'<html')):
            raise ValueError('html_instead_of_asset')
        if target.suffix in ['.js','.css']:
            pending.extend(re.findall(r'''["'(](/home-assets/[^"'()\s]+)''',target.read_text()))
    for name in LICENSES:
        target = root / 'home-assets/licenses' / name
        if not target.is_file() or not target.stat().st_size:
            raise ValueError('missing_license')
    return checked


def verify_http(root, origin):
    for path in ['index.html'] + [p.lstrip('/') for p in validate(root)]:
        with urlopen(origin.rstrip('/') + '/' + path, timeout=30) as response:
            body = response.read(8 * 1024 * 1024 + 1)
            if response.status != 200 or sha256(body).digest() != sha256((root / path).read_bytes()).digest():
                raise ValueError('served_asset_differs_from_image')
            if path.endswith(('.js', '.css')) and response.headers.get_content_type() == 'text/html':
                raise ValueError('spa_fallback_instead_of_asset')


def image_root(image, destination):
    container = subprocess.check_output(['docker', 'create', image], text=True).strip()
    try:
        subprocess.run(['docker', 'cp', container + ':/app/wwwroot/.', str(destination)], check=True)
    finally:
        subprocess.run(['docker', 'rm', container], check=True, stdout=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path)
    parser.add_argument('--image')
    parser.add_argument('--url')
    parser.add_argument('--build', action='store_true')
    parser.add_argument('--tree', default='HEAD', help='Git tree/commit; use git write-tree to verify staged changes')
    parser.add_argument('--tag', default='linernotes-release-check:' + uuid.uuid4().hex)
    args = parser.parse_args()
    if args.build:
        if args.root or args.image:
            parser.error('--build cannot be combined with --root/--image')
        tree = subprocess.check_output(['git', 'rev-parse', '--verify', args.tree + '^{tree}'], cwd=ROOT, text=True).strip()
        with tempfile.TemporaryDirectory(prefix='liner-release-source-') as directory:
            archive = Path(directory) / 'source.tar'
            subprocess.run(['git', 'archive', '--format=tar', '--output', str(archive), tree], cwd=ROOT, check=True)
            source = Path(directory) / 'source'
            source.mkdir()
            with tarfile.open(archive) as contents:
                contents.extractall(source, filter='data')
            subprocess.run(['docker', 'build', '--target', 'web', '--build-arg','LINER_SOURCE_TREE='+tree,'-t', args.tag, str(source)], check=True)
        args.image = args.tag
        print(json.dumps({'sourceTree': tree, 'imageId': subprocess.check_output(
            ['docker', 'image', 'inspect', args.image, '--format', '{{.Id}}'], text=True).strip()}))
    if bool(args.root) == bool(args.image):
        parser.error('select --root, --image or --build')
    with tempfile.TemporaryDirectory(prefix='liner-release-assets-') as directory:
        root = args.root or Path(directory)
        if args.image:
            image_root(args.image, root)
        assets = validate(root)
        if args.url:
            verify_http(root, args.url)
        print(json.dumps({'status': 'passed', 'assets': len(assets), 'licenses': len(LICENSES)}))


if __name__ == '__main__':
    main()
