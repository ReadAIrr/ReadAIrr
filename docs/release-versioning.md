# ReadAIrr Release Versioning

ReadAIrr starts its fork-owned stable release line at `1.0.0`.

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
