# ReadAIrr Container Deployment

This directory contains a small Docker Compose deployment for running this forked
ReadAIrr image on a Docker host.

## Scope

These files deploy the ReadAIrr application, not the public project website.
The project development environment is the `ReadAIrrEggLab` VM hosted by
Eggman, and the QA helper below targets that environment. The public
`readairr.com` site is independently deployed to CT `207` (`readairr-web`) on
`pve-phx-01` behind `pve-phx-edge`; no App workflow deploys or updates that
guest. See [the deployment boundary runbook](../docs/deployment-boundaries.md)
for ownership and handoff rules.

## Files

- `compose.yml` runs ReadAIrr plus a same-VM Postgres database for ReadAIrr.
  Metadata defaults to the hosted Goodreads-compatible service.
- `readarr.env.example` documents the required variables.
- `install-host.sh` prepares the Docker host, adds the NFS media mount when
  configured, and starts the compose project.

## Typical Flow

1. Copy `readarr.env.example` to a host-local env file, such as
   `/etc/readarr/readarr.env`.
2. Fill in the image, config path, port, UID/GID, and optional NFS settings.
   The development image published by this fork is currently
   `ghcr.io/readairr/app:dev`.
   Set `READARR_POSTGRES_PASSWORD` from the host secret store or Azure Key Vault
   before starting the stack.
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
- `val` is published from the `val` branch.
- `prod`, `latest`, `<major>`, and `<major>.<minor>` are published from the
  `prod` branch.
- `v*` tags are published as matching image tags.
- Pull requests build the image but do not publish it.

Container builds use the `<track-version-base>.<github-run-number>` version
family from `docs/release-versioning.json`. Docker and Unraid installs use
Docker image updates instead of the built-in updater; the selected branch
controls both the image tag and the in-app update track.

Release notes for automation live in `docs/release-notes.json`. The same source
is intended to feed public website changelog/update-service entries when a dev
build is promoted.

If this is shared beyond your homelab, make the GHCR package public or provide
users with registry login instructions. For repeatable installs, prefer a
version tag or digest over a moving branch tag.

## Fast Homelab QA Deploys

For quick iteration on the dedicated `ReadAIrrEggLab` VM, use the local QA
deploy helper instead of waiting for GitHub Actions and GHCR:

```sh
deploy/qa-lab-deploy.sh --version 1.0.0.0
```

If the local `readairr:qa-bindings` image is already built, skip the rebuild:

```sh
deploy/qa-lab-deploy.sh --skip-build
```

The helper streams the local image over SSH, updates `READARR_IMAGE` in the
remote env file, recreates only the `readarr` service, and checks
`http://127.0.0.1:8789/ping` from inside the VM. It also resets the app's
internal config port to `8787` before restart, which keeps the Docker host
mapping `8789 -> 8787` intact.

## Metadata Source

ReadAIrr defaults to the hosted Goodreads-compatible metadata service at
`https://api.bookinfo.pro`. `Settings > Development` also offers the hosted
Hardcover-compatible service and a custom rreading-glasses URL. Use a custom URL
only when you operate rreading-glasses yourself, for example
`http://rreading-glasses:8788` on a Docker network shared with the ReadAIrr
container.

The compose stack no longer creates or updates rreading-glasses containers.
Custom metadata services are managed outside ReadAIrr.

The ReadAIrr Postgres password is required by `compose.yml` and should come from
a host-local secret source. The placeholder in `readarr.env.example` is only
there so compose configuration validation can run without a real secret.

## Eggman QA Layout

The example env follows the dedicated Eggman-hosted `ReadAIrrEggLab` VM
convention:

- Compose project files live under `/opt/readairr/dev`.
- ReadAIrr connects to `readarr-db:5432` on the compose network and stores its
  main, log, and cache data in `readarr-main`, `readarr-log`, and
  `readarr-cache`.
- Unraid NFS shares mount on the host under `/mnt/unraid`.
- Container config is a bind mount under `/config`.
- Downloads are available as `/downloads`.
- Audiobook media is available as `/mnt/user/audiobooks`, `/media/audiobooks`,
  and `/audiobooks` for compatibility with different ReadAIrr root-folder
  choices.
- Metadata defaults to the hosted Goodreads-compatible service. If Eggman later
  runs a custom rreading-glasses container, select `Custom rreading-glasses URL`
  in `Settings > Development` and enter the internal Docker/network URL.
