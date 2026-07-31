# ReadAIrr

ReadAIrr is a fork of Readarr focused on audiobook and ebook library management,
metadata resiliency, narrator-aware workflows, and container-first deployment.

ReadAIrr preserves inherited Readarr-compatible API, namespace, path, and
configuration names where the existing application still depends on them. User
facing branding and deployment documentation should use ReadAIrr.

## Features

- Manage audiobook and ebook libraries from a web UI.
- Monitor authors, import existing libraries, and work with supported download
  clients.
- Use hosted Goodreads/Hardcover metadata sources or a custom rreading-glasses URL.
- Review unmatched files with metadata, AI review, and speech-to-text evidence.
- Track and resolve missing narrator evidence.
- Run with the default SQLite database or optional PostgreSQL.
- Deploy with Docker Compose or the Unraid Community Applications template.

## Containers

Images are published to GitHub Container Registry:

```text
ghcr.io/readairr/app:dev
ghcr.io/readairr/app:val
ghcr.io/readairr/app:prod
ghcr.io/readairr/app:latest
```

Use `prod` for normal installs, `val` for validation builds, and `dev` for
active development builds. See [docs/release-versioning.md](docs/release-versioning.md)
for the release-track policy and [docs/dev-release-notes.md](docs/dev-release-notes.md)
for the current dev change list.

## Deployment

This repository builds and packages the ReadAIrr application. Its homelab QA
deployment runs on the Eggman-hosted `ReadAIrrEggLab` VM; it does not deploy the
public `readairr.com` site. The public site is a separate PHX web guest. See
[docs/deployment-boundaries.md](docs/deployment-boundaries.md) before changing
an operator target or handing work to the Site or Docs repository.

- Docker Compose: [deploy/README.md](deploy/README.md)
- Unraid Community Applications: [docs/unraid.md](docs/unraid.md)
- Unraid template: [templates/readairr.xml](templates/readairr.xml)

## License

ReadAIrr is distributed under the GNU GPL v3. See [LICENSE.md](LICENSE.md).
