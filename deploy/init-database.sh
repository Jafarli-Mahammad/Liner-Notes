#!/bin/sh
set -eu
LINER_APPLICATION_PASSWORD=$(cat /run/secrets/application-password)
export LINER_APPLICATION_PASSWORD
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-psqlrc --set ON_ERROR_STOP=1 --file /opt/liner/init-database.sql
unset LINER_APPLICATION_PASSWORD
