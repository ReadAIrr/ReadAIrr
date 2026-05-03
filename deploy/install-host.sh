#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 /path/to/readarr.env" >&2
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

ENV_FILE="${1:-}"
if [[ -z "${ENV_FILE}" || ! -f "${ENV_FILE}" ]]; then
  usage
  exit 64
fi

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run as root so the script can create mounts, directories, and fstab entries." >&2
  exit 77
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-${SCRIPT_DIR}/compose.yml}"

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

: "${READARR_IMAGE:?Set READARR_IMAGE in ${ENV_FILE}}"
: "${READARR_CONFIG_DIR:?Set READARR_CONFIG_DIR in ${ENV_FILE}}"

READARR_UID="${READARR_UID:-1000}"
READARR_GID="${READARR_GID:-1000}"
READARR_MEDIA_DIR="${READARR_MEDIA_DIR:-${AUDIOBOOKS_MOUNT:-}}"
READARR_DOWNLOADS_DIR="${READARR_DOWNLOADS_DIR:-${DOWNLOADS_MOUNT:-}}"

if [[ -z "${READARR_MEDIA_DIR}" ]]; then
  echo "Set READARR_MEDIA_DIR, or set AUDIOBOOKS_MOUNT so it can be reused." >&2
  exit 64
fi

if [[ -z "${READARR_DOWNLOADS_DIR}" ]]; then
  echo "Set READARR_DOWNLOADS_DIR, or set DOWNLOADS_MOUNT so it can be reused." >&2
  exit 64
fi

export READARR_MEDIA_DIR READARR_DOWNLOADS_DIR READARR_UID READARR_GID

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is not installed on this host." >&2
  exit 69
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose v2 is not installed on this host." >&2
  exit 69
fi

install -d -m 0755 "${READARR_CONFIG_DIR}" "${READARR_MEDIA_DIR}" "${READARR_DOWNLOADS_DIR}"
chown "${READARR_UID}:${READARR_GID}" "${READARR_CONFIG_DIR}"

mount_nfs_share() {
  local server="$1"
  local export_path="$2"
  local mount_path="$3"
  local options="$4"

  if [[ -z "${server}" || -z "${export_path}" || -z "${mount_path}" ]]; then
    return 0
  fi

  if ! command -v mount.nfs >/dev/null 2>&1; then
    if command -v apt-get >/dev/null 2>&1; then
      apt-get update
      apt-get install -y nfs-common
    else
      echo "mount.nfs is missing and this script only auto-installs it with apt-get." >&2
      exit 69
    fi
  fi

  install -d -m 0755 "${mount_path}"

  local nfs_source="${server}:${export_path}"

  if ! awk -v src="${nfs_source}" -v dst="${mount_path}" '$1 == src && $2 == dst { found = 1 } END { exit found ? 0 : 1 }' /etc/fstab; then
    printf '%s %s nfs %s 0 0\n' "${nfs_source}" "${mount_path}" "${options}" >> /etc/fstab
  fi

  systemctl daemon-reload >/dev/null 2>&1 || true
  if ! findmnt -rn --target "${mount_path}" >/dev/null 2>&1; then
    mount "${mount_path}"
  fi
}

default_nfs_options="rw,_netdev,nofail"
mount_nfs_share "${AUDIOBOOKS_NFS_SERVER:-}" "${AUDIOBOOKS_NFS_EXPORT:-}" "${AUDIOBOOKS_MOUNT:-}" "${AUDIOBOOKS_NFS_OPTIONS:-${default_nfs_options}}"
mount_nfs_share "${DOWNLOADS_NFS_SERVER:-}" "${DOWNLOADS_NFS_EXPORT:-}" "${DOWNLOADS_MOUNT:-}" "${DOWNLOADS_NFS_OPTIONS:-${default_nfs_options}}"

docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" pull
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d
