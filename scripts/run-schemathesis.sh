#!/bin/bash
export PATH=$PATH:/home/mahammadjafarli/.local/bin
echo "Running Schemathesis for Partner API..."
schemathesis run http://localhost:5074/swagger/partner/swagger.json \
  --checks all \
  --request-timeout 10000 \
  --max-examples 2
