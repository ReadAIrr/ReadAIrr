# ReadAIrr Dev Release Notes

These notes track the active `dev` container line. Stable validation and
production release notes should be cut from this page when a dev build is
promoted.

Automation should read [release-notes.json](release-notes.json). This Markdown
page is the human-friendly companion for the active development line.

## 1.1.0 Dev

Image tag: `ghcr.io/readairr/app:dev`

Assembly version pattern: `1.1.0.<github-run-number>`

### Unmapped STT Narrator Review

- Added a guided Scan STT review modal for unmapped audio files.
- Limited deep audio identification to the first 60 seconds of audio.
- Shows live/polled identification steps while the background STT job runs.
- Surfaces transcript text and an intro audio preview in the review modal.
- Uses the configured LLM provider to suggest a narrator from the transcript and
  available file context.
- Compares suggested narrator spelling against provider metadata where matching
  edition evidence is available.
- Lets the user confirm the suggested or validated narrator before it affects
  manual import review.
- Carries accepted narrator evidence and validated edition hints into the manual
  import modal.

### Operational Notes

- OpenRouter/LLM and STT actions remain opt-in. Normal matching and import flows
  continue to work without an AI provider configured.
- AI/STT results are review evidence only. They do not auto-import files.
- The `dev` branch now uses a separate version base from `val` and `prod` so
  active development builds can move ahead without changing stable track
  numbering.
