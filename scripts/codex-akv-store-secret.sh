#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: scripts/codex-akv-store-secret.sh <toolset> <ENV_NAME>" >&2
}

die() {
  echo "error: $*" >&2
  exit 1
}

need() {
  command -v "$1" >/dev/null 2>&1 || die "$1 is required"
}

repo_root() {
  git rev-parse --show-toplevel 2>/dev/null || pwd
}

registry_path() {
  local root
  local local_registry
  if [[ -n "${CODEX_TOOL_REGISTRY:-}" ]]; then
    echo "${CODEX_TOOL_REGISTRY}"
    return
  fi
  root="$(repo_root)"
  local_registry="${root}/.codex/tool-registry.json"
  if [[ -f "${local_registry}" ]]; then
    echo "${local_registry}"
  else
    echo "${HOME}/.codex/tool-registry.json"
  fi
}

toolset="${1:-}"
env_name="${2:-}"

if [[ -z "${toolset}" || -z "${env_name}" ]]; then
  usage
  exit 64
fi

need jq
need az

registry="$(registry_path)"
[[ -f "${registry}" ]] || die "registry not found: ${registry}"

vault="$(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.vaultName // empty' "${registry}")"
secret_name="$(jq -r --arg toolset "${toolset}" --arg env_name "${env_name}" '.toolsets[$toolset].azureKeyVault.secrets[$env_name] // empty' "${registry}")"
subscription="$(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.subscriptionId // empty' "${registry}")"

[[ -n "${vault}" ]] || die "${toolset} does not declare an Azure Key Vault"
[[ -n "${secret_name}" ]] || die "${toolset} does not map ${env_name} to a Key Vault secret"

printf 'Paste value for %s (%s/%s): ' "${env_name}" "${vault}" "${secret_name}" >&2
IFS= read -r -s secret_value
printf '\n' >&2

[[ -n "${secret_value}" ]] || die "secret value was empty"

az_args=()
if [[ -n "${subscription}" ]]; then
  az_args+=(--subscription "${subscription}")
fi

az keyvault secret set \
  --vault-name "${vault}" \
  --name "${secret_name}" \
  --value "${secret_value}" \
  --content-type "codex-env:${toolset}:${env_name}" \
  --tags "codexStatus=active" "codexToolset=${toolset}" "codexEnv=${env_name}" \
  "${az_args[@]}" \
  --only-show-errors \
  --output none

echo "Stored ${env_name} as ${secret_name} in ${vault}."
