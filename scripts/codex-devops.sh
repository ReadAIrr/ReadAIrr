#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'USAGE'
Usage:
  scripts/codex-devops.sh projects
  scripts/codex-devops.sh hosts
  scripts/codex-devops.sh toolsets
  scripts/codex-devops.sh project <name>
  scripts/codex-devops.sh host <name>
  scripts/codex-devops.sh toolset <name>

Environment:
  CODEX_DEVOPS_REGISTRY  Defaults to ~/.codex/devops-registry.json.
USAGE
}

die() {
  echo "error: $*" >&2
  exit 1
}

need() {
  command -v "$1" >/dev/null 2>&1 || die "$1 is required"
}

registry_path() {
  echo "${CODEX_DEVOPS_REGISTRY:-${HOME}/.codex/devops-registry.json}"
}

main() {
  need jq

  local registry command name
  registry="$(registry_path)"
  [[ -f "${registry}" ]] || die "registry not found: ${registry}"

  command="${1:-}"
  name="${2:-}"

  case "${command}" in
    projects)
      jq -r '.projects | keys[]' "${registry}"
      ;;
    hosts)
      jq -r '.hosts | keys[]' "${registry}"
      ;;
    toolsets)
      jq -r '.toolsets | keys[]' "${registry}"
      ;;
    project)
      [[ -n "${name}" ]] || die "missing project name"
      jq -e --arg name "${name}" '.projects[$name]' "${registry}"
      ;;
    host)
      [[ -n "${name}" ]] || die "missing host name"
      jq -e --arg name "${name}" '.hosts[$name]' "${registry}"
      ;;
    toolset)
      [[ -n "${name}" ]] || die "missing toolset name"
      jq -e --arg name "${name}" '.toolsets[$name]' "${registry}"
      ;;
    -h|--help|help|"")
      usage
      ;;
    *)
      usage
      exit 64
      ;;
  esac
}

main "$@"
