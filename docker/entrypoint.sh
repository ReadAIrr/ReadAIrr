#!/usr/bin/env sh
set -eu

umask "${UMASK:-022}"

if [ "$#" -gt 0 ]; then
  case "$1" in
    -*)
      exec /app/readarr/Readarr -nobrowser -data=/config "$@"
      ;;
    *)
      exec "$@"
      ;;
  esac
fi

exec /app/readarr/Readarr -nobrowser -data=/config
