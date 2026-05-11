# ReadAIrr Release Versioning

ReadAIrr starts its fork-owned stable release line at `1.0.0`. The enforced
source of truth is [release-versioning.json](release-versioning.json).

Container builds stamp the application assembly as:

```text
<track-version-base>.<github-run-number>
```

The fourth segment keeps .NET assembly versions numeric while still increasing
monotonically on every published build.

## Tracks

- `dev`: active development builds from the `dev` branch. Current base:
  `1.1.0`.
- `val`: validation builds promoted from development before production. Current
  base: `1.0.0`.
- `prod`: production builds. Current base: `1.0.0`.

The production branch also publishes `latest`, `1`, and `1.0` image tags.
Version tags such as `v1.0.0` publish a matching container tag.

## Current Dev Line

The current `dev` line is `1.1.0.<github-run-number>`.

This line includes the first guided unmapped-file STT/narrator review workflow:
short intro transcription, live review progress, LLM narrator suggestion,
provider metadata validation, manual confirmation, and narrator evidence carried
into manual import review.

See [dev release notes](dev-release-notes.md) for the user-facing change list.

## Promotion Rules

The container workflow validates release metadata before every image build.

- `dev` may move ahead of validation and production.
- `val` must use the same base version as `dev` before a validation image can
  publish.
- `prod` must use the same base version as `dev` and `val` before a production
  image can publish.
- Release tags such as `v1.1.0` must match the current `prod` base.

When promoting, update [release-versioning.json](release-versioning.json) and
[release-notes.json](release-notes.json) in the same change as the branch merge.
The workflow fails if the release note entry does not match the target track's
version base.

## Release Notes Automation

[release-notes.json](release-notes.json) is the machine-readable source for
automation. It includes separate `app`, `container`, `website`, and
`automation` sections so the public website can render friendly release notes
while update-service automation can consume stable IDs, tracks, version bases,
and promotion flags.

## Docker and Unraid Updates

Docker and Unraid installs update by pulling a newer image on the selected tag.
They should not use the built-in updater. Runtime configuration sets:

```text
Readarr__Update__Mechanism=Docker
Readarr__Update__Automatically=false
Readarr__Update__Branch=<dev|val|prod>
```

The selected branch should match the image tag so the app status page and update
guidance describe the same release track the container is actually running.
