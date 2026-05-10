#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'USAGE'
Usage:
  scripts/codex-akv.sh toolsets
  scripts/codex-akv.sh describe <toolset>
  scripts/codex-akv.sh check <toolset>
  scripts/codex-akv.sh placeholder <toolset> <ENV_NAME>
  scripts/codex-akv.sh ensure-placeholders <toolset>
  scripts/codex-akv.sh run <toolset> -- <trusted command...>

Environment:
  CODEX_TOOL_REGISTRY  Override registry path. Defaults to local .codex/tool-registry.json,
                       then $HOME/.codex/tool-registry.json.
USAGE
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

toolset_json() {
  local registry="$1"
  local toolset="$2"
  jq -e --arg toolset "${toolset}" '.toolsets[$toolset]' "${registry}"
}

az_args_for_toolset() {
  local registry="$1"
  local toolset="$2"
  local subscription
  subscription="$(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.subscriptionId // empty' "${registry}")"
  if [[ -n "${subscription}" ]]; then
    printf '%s\n' --subscription
    printf '%s\n' "${subscription}"
  fi
}

vault_for_toolset() {
  local registry="$1"
  local toolset="$2"
  toolset_json "${registry}" "${toolset}" >/dev/null
  jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.vaultName // empty' "${registry}"
}

placeholder_value() {
  printf '%s\n' "__CODEX_PLACEHOLDER_UPDATE_MANUALLY__"
}

show_secret_metadata() {
  local registry="$1"
  local toolset="$2"
  local vault="$3"
  local secret_name="$4"
  az keyvault secret show \
    --vault-name "${vault}" \
    --name "${secret_name}" \
    $(az_args_for_toolset "${registry}" "${toolset}") \
    --query '{id:id, contentType:contentType, tags:tags}' \
    --output json \
    --only-show-errors
}

is_placeholder_metadata() {
  local metadata="$1"
  jq -e '(.tags.codexStatus // "") == "placeholder" or ((.contentType // "") | startswith("codex-placeholder:"))' >/dev/null <<<"${metadata}"
}

not_ready_reason() {
  local metadata="$1"
  jq -r '
    if (.tags.codexStatus // "") == "placeholder" or ((.contentType // "") | startswith("codex-placeholder:")) then
      "placeholder"
    elif (.tags.codexStatus // "") == "needs-permission" then
      "needs-permission"
    elif (.tags.codexStatus // "") == "invalid" then
      "invalid"
    else
      ""
    end
  ' <<<"${metadata}"
}

store_secret_command() {
  if [[ -x "${HOME}/.codex/bin/codex-akv-store-secret" ]]; then
    printf '%s\n' "${HOME}/.codex/bin/codex-akv-store-secret"
  else
    printf '%s\n' "scripts/codex-akv-store-secret.sh"
  fi
}

cmd_toolsets() {
  local registry="$1"
  jq -r '.toolsets | keys[]' "${registry}"
}

cmd_describe() {
  local registry="$1"
  local toolset="$2"
  toolset_json "${registry}" "${toolset}"
}

cmd_check() {
  local registry="$1"
  local toolset="$2"
  local vault missing_count metadata reason

  vault="$(vault_for_toolset "${registry}" "${toolset}")"
  [[ -n "${vault}" ]] || die "${toolset} does not declare an Azure Key Vault"

  missing_count=0
  while IFS=$'\t' read -r env_name secret_name; do
    if ! metadata="$(show_secret_metadata "${registry}" "${toolset}" "${vault}" "${secret_name}" 2>/dev/null)"; then
      echo "missing or inaccessible: ${env_name} -> ${secret_name}"
      missing_count=$((missing_count + 1))
    else
      reason="$(not_ready_reason "${metadata}")"
      if [[ -n "${reason}" ]]; then
        case "${reason}" in
          placeholder)
            echo "placeholder needs manual value: ${env_name} -> ${secret_name}"
            echo "  update with: $(store_secret_command) ${toolset} ${env_name}"
            ;;
          needs-permission)
            echo "secret value exists but needs corrected permissions: ${env_name} -> ${secret_name}"
            ;;
          invalid)
            echo "secret value exists but failed validation: ${env_name} -> ${secret_name}"
            ;;
        esac
        missing_count=$((missing_count + 1))
      fi
    fi
  done < <(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.secrets // {} | to_entries[] | [.key, .value] | @tsv' "${registry}")

  if [[ "${missing_count}" -ne 0 ]]; then
    exit 1
  fi

  echo "${toolset}: Azure Key Vault reachable; all mapped secret name(s) are present."
}

cmd_placeholder() {
  local registry="$1"
  local toolset="$2"
  local env_name="$3"
  local vault secret_name metadata

  vault="$(vault_for_toolset "${registry}" "${toolset}")"
  [[ -n "${vault}" ]] || die "${toolset} does not declare an Azure Key Vault"

  secret_name="$(jq -r --arg toolset "${toolset}" --arg env_name "${env_name}" '.toolsets[$toolset].azureKeyVault.secrets[$env_name] // empty' "${registry}")"
  [[ -n "${secret_name}" ]] || die "${toolset} does not map ${env_name} to a Key Vault secret"

  if metadata="$(show_secret_metadata "${registry}" "${toolset}" "${vault}" "${secret_name}" 2>/dev/null)"; then
    if is_placeholder_metadata "${metadata}"; then
      echo "${env_name} already has a placeholder: ${secret_name}"
      echo "Update it manually with: $(store_secret_command) ${toolset} ${env_name}"
      return
    fi
    echo "${env_name} already exists and is not marked as a placeholder: ${secret_name}"
    return
  fi

  az keyvault secret set \
    --vault-name "${vault}" \
    --name "${secret_name}" \
    --value "$(placeholder_value)" \
    --content-type "codex-placeholder:${toolset}:${env_name}" \
    --tags "codexStatus=placeholder" "codexToolset=${toolset}" "codexEnv=${env_name}" \
    $(az_args_for_toolset "${registry}" "${toolset}") \
    --only-show-errors \
    --output none

  echo "Created placeholder for ${env_name}: ${secret_name}"
  echo "Update it manually with: $(store_secret_command) ${toolset} ${env_name}"
}

cmd_ensure_placeholders() {
  local registry="$1"
  local toolset="$2"
  local vault

  vault="$(vault_for_toolset "${registry}" "${toolset}")"
  [[ -n "${vault}" ]] || die "${toolset} does not declare an Azure Key Vault"

  while IFS=$'\t' read -r env_name _secret_name; do
    cmd_placeholder "${registry}" "${toolset}" "${env_name}"
  done < <(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.secrets // {} | to_entries[] | [.key, .value] | @tsv' "${registry}")
}

cmd_run() {
  local registry="$1"
  local toolset="$2"
  shift 2

  [[ "${1:-}" == "--" ]] || die "run requires -- before the command"
  shift
  [[ "$#" -gt 0 ]] || die "missing command"

  local vault metadata reason
  vault="$(vault_for_toolset "${registry}" "${toolset}")"
  [[ -n "${vault}" ]] || die "${toolset} does not declare an Azure Key Vault"

  while IFS=$'\t' read -r env_name secret_name; do
    if ! metadata="$(show_secret_metadata "${registry}" "${toolset}" "${vault}" "${secret_name}" 2>/dev/null)"; then
      die "${env_name} -> ${secret_name} is missing or inaccessible"
    fi
    reason="$(not_ready_reason "${metadata}")"
    if [[ -n "${reason}" ]]; then
      case "${reason}" in
        placeholder)
          die "${env_name} -> ${secret_name} is still a placeholder. Update manually with: $(store_secret_command) ${toolset} ${env_name}"
          ;;
        needs-permission)
          die "${env_name} -> ${secret_name} exists but needs corrected permissions before use"
          ;;
        invalid)
          die "${env_name} -> ${secret_name} exists but failed validation"
          ;;
      esac
    fi
    value="$(az keyvault secret show \
      --vault-name "${vault}" \
      --name "${secret_name}" \
      $(az_args_for_toolset "${registry}" "${toolset}") \
      --query value \
      --output tsv \
      --only-show-errors)"
    if [[ "${value}" == "$(placeholder_value)" ]]; then
      die "${env_name} -> ${secret_name} still contains the placeholder value. Update manually with: $(store_secret_command) ${toolset} ${env_name}"
    fi
    export "${env_name}=${value}"
  done < <(jq -r --arg toolset "${toolset}" '.toolsets[$toolset].azureKeyVault.secrets // {} | to_entries[] | [.key, .value] | @tsv' "${registry}")

  exec "$@"
}

main() {
  local registry command
  registry="$(registry_path)"
  [[ -f "${registry}" ]] || die "registry not found: ${registry}"

  need jq

  command="${1:-}"
  case "${command}" in
    toolsets)
      cmd_toolsets "${registry}"
      ;;
    describe)
      [[ -n "${2:-}" ]] || die "missing toolset"
      cmd_describe "${registry}" "$2"
      ;;
    check|placeholder|ensure-placeholders|run)
      need az
      [[ -n "${2:-}" ]] || die "missing toolset"
      case "${command}" in
        check)
          cmd_check "${registry}" "$2"
          ;;
        placeholder)
          [[ -n "${3:-}" ]] || die "missing ENV_NAME"
          cmd_placeholder "${registry}" "$2" "$3"
          ;;
        ensure-placeholders)
          cmd_ensure_placeholders "${registry}" "$2"
          ;;
        run)
          shift
          cmd_run "${registry}" "$@"
          ;;
      esac
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
