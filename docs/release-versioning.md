# ReadAIrr Release Versioning

ReadAIrr starts its fork-owned release line at `1.0.0`.

Container builds stamp the application assembly as:

```text
1.0.0.<github-run-number>
```

The fourth segment keeps .NET assembly versions numeric while still increasing
monotonically on every published build.

## Tracks

- `dev`: active development builds from the `dev` branch.
- `val`: validation builds promoted from development before production.
- `prod`: production builds.

The production branch also publishes `latest`, `1`, and `1.0` image tags.
Version tags such as `v1.0.0` publish a matching container tag.

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
