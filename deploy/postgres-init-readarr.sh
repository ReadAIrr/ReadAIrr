#!/usr/bin/env bash
set -euo pipefail

for db_name in "${POSTGRES_DB}" "${READARR_POSTGRES_LOG_DB}" "${READARR_POSTGRES_CACHE_DB}"; do
  if [[ -z "${db_name}" ]]; then
    continue
  fi

  psql -v ON_ERROR_STOP=1 --username "${POSTGRES_USER}" --dbname postgres -v db="${db_name}" <<'EOSQL'
SELECT format('CREATE DATABASE %I', :'db')
WHERE NOT EXISTS (
  SELECT 1
  FROM pg_database
  WHERE datname = :'db'
)\gexec
EOSQL
done
