# ReadAIrr on Unraid

This template installs ReadAIrr from `ghcr.io/readairr/app`.

## Release Tracks

Use `prod` for normal installs. `val` is for validation builds before
production promotion. `dev` is for active development builds.

To select a track in Unraid, set both:

- `Repository`: `ghcr.io/readairr/app:prod`, `ghcr.io/readairr/app:val`, or `ghcr.io/readairr/app:dev`
- `Release Track`: `prod`, `val`, or `dev`

Unraid/Docker installs should use Docker image updates. The template sets
`Readarr__Update__Mechanism=Docker` and disables built-in automatic updates.

## Paths

Use `/audiobooks` as the normal ReadAIrr root folder. The template also exposes
advanced compatibility mounts for `/media/audiobooks` and
`/mnt/user/audiobooks` so existing imports can be aligned without rebuilding the
container.

## Optional PostgreSQL

SQLite is the default when `PostgreSQL Host` is blank.

To use PostgreSQL, create three databases and a user before starting ReadAIrr:

```sql
CREATE DATABASE "readarr-main";
CREATE DATABASE "readarr-log";
CREATE DATABASE "readarr-cache";
CREATE USER readarr WITH PASSWORD 'replace-this-password';
GRANT ALL PRIVILEGES ON DATABASE "readarr-main" TO readarr;
GRANT ALL PRIVILEGES ON DATABASE "readarr-log" TO readarr;
GRANT ALL PRIVILEGES ON DATABASE "readarr-cache" TO readarr;
```

Then set the advanced PostgreSQL fields in the template. For a Postgres
container on the same Unraid host, put both containers on the same custom Docker
network and set `PostgreSQL Host` to that Postgres container name.

## Metadata and STT

Metadata source, matching confidence, OpenRouter, and speech-to-text settings
are configured inside ReadAIrr under `Settings > Development`. They are stored
in ReadAIrr app data rather than in the Docker template so API keys are not
exposed in the Unraid template XML.

New installs default to the hosted Goodreads-compatible metadata service. The
Development page also offers the hosted Hardcover-compatible service and a
custom rreading-glasses URL. Use a custom URL only when you operate
rreading-glasses yourself, for example `http://rreading-glasses:8788` on a
Docker network reachable from the ReadAIrr container.

## Manual Test Containers

If you create or recreate a ReadAIrr container with `docker run` instead of the
Unraid Docker UI, add Unraid's Docker Manager labels yourself. Without these
labels the container can run correctly but the Docker tab may not show the
WebUI shortcut.

```sh
docker run -d \
  --name readairr-test \
  --network eth0 \
  --ip 192.168.0.228 \
  --restart unless-stopped \
  --label net.unraid.docker.managed=dockerman \
  --label 'net.unraid.docker.webui=http://[IP]:[PORT:8787]' \
  --label net.unraid.docker.icon=https://raw.githubusercontent.com/ReadAIrr/App/prod/Logo/512.png \
  --user 99:100 \
  -v /mnt/user/appdata/readairr-test:/config \
  -v /mnt/user/downloads:/downloads \
  -v /mnt/user/audiobooks:/audiobooks \
  -e Readarr__Server__BindAddress='*' \
  -e Readarr__Server__Port=8787 \
  -e Readarr__Update__Mechanism=Docker \
  -e Readarr__Update__Automatically=false \
  -e Readarr__Update__Branch=dev \
  ghcr.io/readairr/app:dev
```
