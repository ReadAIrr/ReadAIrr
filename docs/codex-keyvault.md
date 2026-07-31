# Codex Azure Key Vault Secrets

This repo uses a non-secret Codex tool registry at `.codex/tool-registry.json`.
The registry records which tools apply to this project and which Azure Key
Vault secret names provide their credentials. It must not contain secret values.

## Azure Resources

- Subscription: `TvR` (`fb45451c-66aa-44de-9c41-35d17665ff46`)
- Resource group: `rg-shared-services`
- Location: `westus2`
- Key Vault: `tvr-shared-tv001-kv`
- Vault URI: `https://tvr-shared-tv001-kv.vault.azure.net/`
- Authorization: Azure RBAC
- Soft delete retention: 90 days
- Purge protection: off for the initial setup

The signed-in Azure user has `Key Vault Secrets Officer` on this vault.

## Deployment Boundary

Secret routing does not imply deployment ownership. This App repository
publishes the ReadAIrr container image and contains deployment helpers for the
Eggman-hosted `ReadAIrrEggLab` development environment. The public
`readairr.com` site is a separate deployment on PHX CT `207` (`readairr-web`)
and must not be treated as an App runtime target. Use
[deployment-boundaries.md](deployment-boundaries.md) for the current topology
and the separate Site/Docs handoff.

## Registry Layout

The starter registry expects these Key Vault secrets:

- `ghcr-readiarr`: `ghcr-readiarr-username`, `ghcr-readiarr-token`
- `readairr-egglab`: `readairr-egglab-deploy-host`, `readairr-egglab-deploy-user`
- `cloudflare-codex`: `cloudflare-codex-access-token`
- `cloudflare-dns`: `cloudflare-dns-edit-token`

Each toolset maps environment variable names to Key Vault secret names. For
example, `GHCR_TOKEN` maps to `ghcr-readiarr-token`.

## Helper Commands

List registry toolsets:

```sh
scripts/codex-akv.sh toolsets
```

Show non-secret details for a toolset:

```sh
scripts/codex-akv.sh describe ghcr-readiarr
```

Check that every mapped secret exists, without printing secret values:

```sh
scripts/codex-akv.sh check ghcr-readiarr
```

Create a placeholder for a required secret when the value is not available yet:

```sh
scripts/codex-akv.sh placeholder ghcr-readiarr GHCR_TOKEN
```

Create placeholders for every missing mapped secret in a toolset:

```sh
scripts/codex-akv.sh ensure-placeholders ghcr-readiarr
```

Store or update one mapped secret. The value is read silently:

```sh
scripts/codex-akv-store-secret.sh ghcr-readiarr GHCR_TOKEN
```

Run a trusted command with one toolset's Key Vault secrets injected as
environment variables:

```sh
scripts/codex-akv.sh run ghcr-readiarr -- sh -c 'printf "%s" "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USERNAME" --password-stdin'
```

Only use `run` with commands you already trust, because the command receives
the secrets as environment variables.

## Placeholder Rule

When a required credential is missing, create a Key Vault placeholder immediately
and tell Toby exactly which value to update manually. Do not wait for the real
secret value in chat, and do not write real secret values to repo files.

Placeholders use the value `__CODEX_PLACEHOLDER_UPDATE_MANUALLY__`, the content
type `codex-placeholder:<toolset>:<ENV_NAME>`, and the tag
`codexStatus=placeholder`. The helper treats placeholders as not ready: `check`
reports them and `run` refuses to inject them.

For mapped toolset secrets, prefer:

```sh
scripts/codex-akv.sh placeholder <toolset> <ENV_NAME>
```

Then instruct Toby to run:

```sh
scripts/codex-akv-store-secret.sh <toolset> <ENV_NAME>
```

For a secret that is only present in the broader DevOps registry, create the
placeholder directly in the shared vault:

```sh
az keyvault secret set \
  --vault-name tvr-shared-tv001-kv \
  --name <secret-name> \
  --value "__CODEX_PLACEHOLDER_UPDATE_MANUALLY__" \
  --content-type "codex-placeholder:<scope>:<key>" \
  --tags codexStatus=placeholder
```

For a cross-repo registry, use the global registry at:

```sh
/Users/toby/.codex/tool-registry.json
```

You can point helpers at that file explicitly:

```sh
CODEX_TOOL_REGISTRY="$HOME/.codex/tool-registry.json" scripts/codex-akv.sh toolsets
```

For broader DevOps context, including project ownership, hosting targets, and
CI/CD entrypoints, use the local global registry:

```sh
/Users/toby/.codex/devops-registry.json
```

The helper can query it from any repo:

```sh
/Users/toby/.codex/bin/codex-devops projects
/Users/toby/.codex/bin/codex-devops project readairr-app
```

The Key Vault helpers are also installed globally:

```sh
/Users/toby/.codex/bin/codex-akv toolsets
/Users/toby/.codex/bin/codex-akv-store-secret ghcr-readiarr GHCR_TOKEN
```

When run outside a repo-local registry, they fall back to
`/Users/toby/.codex/tool-registry.json`.

## Cloudflare

The `cloudflare-codex` toolset stores the current local Cloudflare API token in
Key Vault as `cloudflare-codex-access-token`. As of 2026-05-04, that token is
active and can verify itself, list the Cloudflare account, and list zones. It
does not have DNS-record access; DNS record listing returned `403 Authentication
error` for every visible zone.

Use `cloudflare-dns-edit-token` for DNS read/write automation. As of
2026-05-04, the current value verifies as an active Cloudflare token, can list
DNS records across the visible zones, and passed a temporary TXT record
create/update/delete test on `readairr.com`.

Update it manually if the token is rotated:

```sh
/Users/toby/.codex/bin/codex-akv-store-secret cloudflare-dns CLOUDFLARE_DNS_EDIT_TOKEN
```

## Host Details

Put host topology in the DevOps registry and secret material in Key Vault.

Good registry fields:

- Public IP, LAN IP, Tailscale IP
- Hostname and aliases
- Role, OS, hypervisor/platform
- SSH username, port, and preferred route
- VM IDs, app ownership, deploy paths, and health checks

Keep these as Key Vault secret names only:

- SSH passwords
- Private keys
- API tokens
- Certificate/key contents
- Any `.env` value that grants access

Example App-owned mapping for the Eggman development environment:

```json
"access": {
  "method": "ssh",
  "hostRef": "eggman",
  "guest": "ReadAIrrEggLab",
  "secretRefs": {
    "READAIRR_EGGLAB_HOST": "readairr-egglab-deploy-host",
    "READAIRR_EGGLAB_USER": "readairr-egglab-deploy-user"
  }
}
```

PHX platform access and public-site deployment credentials belong to their
separate operator toolsets; do not add them to an App deployment mapping merely
because application code consumes `readairr.com` URLs.
