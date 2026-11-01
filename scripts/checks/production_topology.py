#!/usr/bin/env python3
"""Check the resolved public and loopback Production topologies without printing private configuration."""
import argparse
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def resolve(acceptance=False):
    env = dict(os.environ, LINER_SITE='music.example.test', LINER_PRIVATE_DIR='/private/liner-check',
               LINER_PROVIDER_USE_APPROVED='false', LINER_WEB_IMAGE='fixture-web', LINER_WORKER_IMAGE='fixture-worker',
               LINER_TOOLS_IMAGE='fixture-tools', LINER_VOLUME_IMAGE='fixture-init', LINER_ACCEPTANCE_PORT='8443')
    args = ['docker', 'compose', '-f', str(ROOT / 'deploy/compose.production.yaml')]
    if acceptance:
        args += ['-f', str(ROOT / 'deploy/compose.acceptance.yaml')]
    result = subprocess.run(args + ['--profile', 'operator', 'config', '--format', 'json'],
                            env=env, capture_output=True, text=True, timeout=30)
    if result.returncode:
        raise ValueError('production_compose_configuration_failed')
    return json.loads(result.stdout)


def validate(config, acceptance=False):
    services = config['services']
    for name, service in services.items():
        if name != 'proxy' and service.get('ports'):
            raise ValueError('private_service_port_published')
        if not service.get('mem_limit') or not service.get('cpus'):
            raise ValueError('service_resources_unbounded')
        if service.get('logging', {}).get('options', {}).get('max-size') != '10m':
            raise ValueError('service_logs_unbounded')
    for name in ['web', 'worker']:
        service = services[name]
        settings = service['environment']
        if settings['ASPNETCORE_ENVIRONMENT'] != 'Production' or settings['Generation__Origin'] != 'Live':
            raise ValueError('development_runtime_in_production')
        if settings['DiscoveryRuntime__EnforceStorageAdmission'] != 'true' or settings['LocalEmail__Enabled'] != 'false':
            raise ValueError('unsafe_production_writes')
        if not service.get('read_only') or service.get('cap_drop') != ['ALL']:
            raise ValueError('runtime_hardening_missing')
        if any(key.startswith(('Jwt__', 'ConnectionStrings__')) for key in settings):
            raise ValueError('secrets_in_expanded_environment')
    ports = services['proxy']['ports']
    if acceptance:
        if len(ports) != 1 or ports[0].get('host_ip') != '127.0.0.1':
            raise ValueError('acceptance_not_loopback_only')
    elif {p['target'] for p in ports} != {80, 443}:
        raise ValueError('unexpected_public_ports')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', required=True)
    parser.parse_args()
    for acceptance in [False, True]:
        validate(resolve(acceptance), acceptance)
    print('Production and loopback acceptance topology checks passed.')
