# Readarr Container Deployment

This directory contains a small Docker Compose deployment for running this forked
Readarr image on a Docker host.

## Files

- `compose.yml` runs Readarr plus an automatic self-hosted
  `rreading-glasses` metadata service and its Postgres database.
- `readarr.env.example` documents the required variables.
- `install-host.sh` prepares the Docker host, adds the NFS media mount when
  configured, and starts the compose project.

## Typical Flow

1. Copy `readarr.env.example` to a host-local env file, such as
   `/etc/readarr/readarr.env`.
2. Fill in the image, config path, port, UID/GID, and optional NFS settings.
   The development image published by this fork is currently
   `ghcr.io/readairr/app:dev`.
   Override the `RREADING_GLASSES_*` variables if you want to change the local
   metadata sidecar settings. Set `RREADING_GLASSES_POSTGRES_PASSWORD` from the
   host secret store or Azure Key Vault before starting the stack.
3. Run:

   ```sh
   sudo ./deploy/install-host.sh /etc/readarr/readarr.env
   ```

The env file is intentionally ignored by git so local storage paths and private
hostnames do not need to be committed.

## Image Publishing

The GitHub Actions container workflow publishes multi-architecture images to
GitHub Container Registry using the current repository path. For this moved
repo, that means `ghcr.io/readairr/app`.

- `dev` is published from the `dev` branch.
- `prod` and `latest` are published from the `prod` branch.
- `v*` tags are published as matching image tags.
- Pull requests build the image but do not publish it.

If this is shared beyond your homelab, make the GHCR package public or provide
users with registry login instructions. For repeatable installs, prefer a
version tag or digest over a moving branch tag.

## Fast Homelab QA Deploys

For quick iteration on the dedicated `ReadAIrrEggLab` VM, use the local QA
deploy helper instead of waiting for GitHub Actions and GHCR:

```sh
deploy/qa-lab-deploy.sh --version 0.4.19.19
```

If the local `readairr:qa-bindings` image is already built, skip the rebuild:

```sh
deploy/qa-lab-deploy.sh --skip-build
```

The helper streams the local image over SSH, updates `READARR_IMAGE` in the
remote env file, recreates only the `readarr` service, leaves the
`rreading-glasses` sidecars running, and checks `http://127.0.0.1:8789/ping`
from inside the VM. It also resets the app's internal config port to `8787`
before restart, which keeps the Docker host mapping `8789 -> 8787` intact.

## Sidecar Image Pinning

The example env pins the `rreading-glasses` and Postgres sidecar images by
digest. That keeps homelab redeploys repeatable even if upstream tags move.
When intentionally refreshing those sidecars, inspect the new image digests and
update `RREADING_GLASSES_IMAGE` or `RREADING_GLASSES_POSTGRES_IMAGE` in the host
env file at the same time you update `readarr.env.example`.

The rreading-glasses Postgres password is required by `compose.yml` and should
come from a host-local secret source. The placeholder in `readarr.env.example`
is only there so compose configuration validation can run without a real
secret.

## Eggman QA Layout

The example env follows the dedicated Eggman-hosted `ReadAIrrEggLab` VM
convention:

- Compose project files live under `/opt/readairr/dev`.
- Unraid NFS shares mount on the host under `/mnt/unraid`.
- Container config is a bind mount under `/config`.
- Downloads are available as `/downloads`.
- Audiobook media is available as `/mnt/user/audiobooks`, `/media/audiobooks`,
  and `/audiobooks` for compatibility with different Readarr root-folder
  choices.
- Selecting `Automatic self-hosted rreading-glasses` in
  `Settings > Development` points Readarr at `http://rreading-glasses:8788`,
  the compose-network address for the local sidecar. The sidecar is not exposed
  on the Docker host because Readarr only needs internal network access to it.
