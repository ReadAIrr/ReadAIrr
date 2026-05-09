#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: deploy/qa-lab-deploy.sh [options]

Build and deploy a local QA image to the ReadAIrrEggLab VM without waiting for
GitHub Actions or GHCR.

Options:
  --skip-build          Deploy an already-built local image tag.
  --tag TAG             Local/remote image tag to deploy. Default: readairr:qa-bindings
  --version VERSION     Numeric Readarr version for Docker build. Default: 0.4.19.19
  --host HOST           SSH host. Default: 192.168.0.61
  --user USER           SSH user. Default: toby
  --ssh-key PATH        SSH identity file. Default: ~/.ssh/id_ed25519
  --remote-dir PATH     Remote compose directory. Default: /opt/readairr/dev
  --container NAME      Container name for logs/status. Default: readarr-dev
  -h, --help            Show this help.

Environment overrides with the same names are also supported:
READARR_QA_TAG, READARR_VERSION, READAIRR_LAB_HOST, READAIRR_LAB_USER,
READAIRR_LAB_SSH_KEY, READAIRR_LAB_REMOTE_DIR, READAIRR_LAB_CONTAINER.
USAGE
}

build_image=1
tag="${READARR_QA_TAG:-readairr:qa-bindings}"
version="${READARR_VERSION:-0.4.19.19}"
host="${READAIRR_LAB_HOST:-192.168.0.61}"
user="${READAIRR_LAB_USER:-toby}"
ssh_key="${READAIRR_LAB_SSH_KEY:-$HOME/.ssh/id_ed25519}"
remote_dir="${READAIRR_LAB_REMOTE_DIR:-/opt/readairr/dev}"
container="${READAIRR_LAB_CONTAINER:-readarr-dev}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-build)
      build_image=0
      shift
      ;;
    --tag)
      tag="$2"
      shift 2
      ;;
    --version)
      version="$2"
      shift 2
      ;;
    --host)
      host="$2"
      shift 2
      ;;
    --user)
      user="$2"
      shift 2
      ;;
    --ssh-key)
      ssh_key="$2"
      shift 2
      ;;
    --remote-dir)
      remote_dir="$2"
      shift 2
      ;;
    --container)
      container="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ ! "$version" =~ ^[0-9]+[.][0-9]+[.][0-9]+[.][0-9]+$ ]]; then
  echo "READARR_VERSION must be numeric, for example 0.4.19.19" >&2
  exit 2
fi

ssh_target="${user}@${host}"
ssh_opts=(
  -o BatchMode=yes
  -o ConnectTimeout=8
  -o IdentitiesOnly=yes
  -i "$ssh_key"
)

if [[ "$build_image" -eq 1 ]]; then
  echo "Building ${tag} with READARR_VERSION=${version}"
  docker build \
    --platform linux/amd64 \
    -t "$tag" \
    --build-arg "READARR_VERSION=${version}" \
    --build-arg BUILD_SOURCEBRANCHNAME=dev \
    .
else
  echo "Skipping build; deploying existing local image ${tag}"
  docker image inspect "$tag" >/dev/null
fi

echo "Streaming ${tag} to ${ssh_target}:${remote_dir}"
remote_tag="$(printf '%q' "$tag")"
remote_compose_dir="$(printf '%q' "$remote_dir")"
remote_container="$(printf '%q' "$container")"

docker save "$tag" | ssh "${ssh_opts[@]}" "$ssh_target" "
set -euo pipefail
tag=${remote_tag}
remote_dir=${remote_compose_dir}
container=${remote_container}

docker load

cd \"\$remote_dir\"

if [ -f config/config.xml ] && ! grep -q '<Port>8787</Port>' config/config.xml; then
  cp config/config.xml \"config/config.xml.pre-qa-deploy.\$(date +%Y%m%d%H%M%S)\"
  sed -i 's|<Port>[0-9][0-9]*</Port>|<Port>8787</Port>|' config/config.xml
fi

if grep -q '^READARR_IMAGE=' .env; then
  sed -i.bak \"s|^READARR_IMAGE=.*|READARR_IMAGE=\${tag}|\" .env
else
  printf '\nREADARR_IMAGE=%s\n' \"\$tag\" >> .env
fi

docker compose --env-file .env -f compose.yml up -d --no-deps readarr

for _ in \$(seq 1 20); do
  if curl --fail --silent --output /dev/null http://127.0.0.1:8789/ping; then
    docker compose --env-file .env -f compose.yml ps readarr
    exit 0
  fi
  sleep 3
done

docker logs --tail 180 \"\$container\" || true
exit 1
"

echo "QA deploy complete: http://${host}:8789/ping"
