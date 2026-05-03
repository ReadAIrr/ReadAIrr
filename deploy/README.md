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
   The development image published by this fork is
   `ghcr.io/tvanroo/readarr:dev`.
   Override the `RREADING_GLASSES_*` variables if you want to change the local
   metadata sidecar settings.
3. Run:

   ```sh
   sudo ./deploy/install-host.sh /etc/readarr/readarr.env
   ```

The env file is intentionally ignored by git so local storage paths and private
hostnames do not need to be committed.

## Arrs VM Layout

The example env follows the existing `arrs` VM convention:

- Compose project files live under `/opt/arrs`.
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
