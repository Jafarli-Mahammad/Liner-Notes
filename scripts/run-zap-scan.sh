#!/bin/bash
echo "Running OWASP ZAP baseline scan..."
docker run --rm -v $(pwd):/zap/wrk/:rw -t ghcr.io/zaproxy/zaproxy:stable zap-baseline.py \
    -t http://host.docker.internal:5074/swagger/partner/swagger.json \
    -r zap-report.html || true
