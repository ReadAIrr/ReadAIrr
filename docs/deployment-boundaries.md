# ReadAIrr Deployment Boundaries

Use this runbook before changing a deployment target, workflow, or operator
credential reference. Re-verify live topology before any runtime mutation.

## Current Surfaces

| Surface | Owner | Deployment boundary | App repository responsibility |
| --- | --- | --- | --- |
| Application image | `ReadAIrr/App` | `ghcr.io/readairr/app` | Build and publish container images from the `dev`, `val`, and `prod` release lines. Image publication does not deploy a running environment. |
| Development application | `ReadAIrr/App` | `ReadAIrrEggLab` VM on Eggman | Maintain the Compose files and QA deployment helper in `deploy/`. This is the project development environment, not the public website. |
| Public website | `ReadAIrr/Site` | CT `207` (`readairr-web`) on `pve-phx-01`; `phxpriv` `10.20.0.53`, `phxout` `10.21.0.53`; public traffic enters through `pve-phx-edge` | Consume the public URLs from application code where required. Do not deploy or reconfigure the site from this repository. |
| Public documentation content | `ReadAIrr/Docs`, with hosting integration in `ReadAIrr/Site` | Served under the public `readairr.com` boundary | Link to the documentation. Coordinate content or hosting changes as a separate repository handoff. |

The PHX web guest is a static-site and public-service boundary. It is not a
relocation of the ReadAIrr application runtime. In particular, the Eggman
development VM remains the target of `deploy/qa-lab-deploy.sh` and the example
Compose configuration.

## Repository Handoffs

- Changes to the application, container image, Compose examples, or Eggman QA
  workflow stay in `ReadAIrr/App`.
- Changes to the public website deployment workflow, web-guest target,
  `readairr.com` hosting behavior, or hosted `/v1/update/` behavior belong in
  `ReadAIrr/Site`.
- Changes to public documentation content belong in `ReadAIrr/Docs`; coordinate
  with `ReadAIrr/Site` when the change also affects how that content is hosted.
- Release metadata in `docs/release-notes.json` is App-owned source data. Its
  publication on the public site is a cross-repository handoff, not an App
  runtime deployment.

Do not change DNS, Cloudflare, `pve-phx-edge`, or CT `207` from this repository.
Any such live operation requires an explicit, separately scoped approval and a
fresh topology check.
