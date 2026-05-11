#!/usr/bin/env bash
set -euo pipefail

notes_path="${1:-docs/release-notes.json}"
version_path="${2:-docs/release-versioning.json}"
target_track="${READAIRR_RELEASE_TRACK:-${GITHUB_BASE_REF:-${GITHUB_REF_NAME:-dev}}}"
ref_type="${READAIRR_REF_TYPE:-${GITHUB_REF_TYPE:-branch}}"
ref_name="${READAIRR_REF_NAME:-${GITHUB_REF_NAME:-$target_track}}"

python3 - "$notes_path" "$version_path" "$target_track" "$ref_type" "$ref_name" <<'PY'
import json
import re
import sys

notes_path, version_path, target_track, ref_type, ref_name = sys.argv[1:6]

with open(notes_path, "r", encoding="utf-8") as handle:
    notes = json.load(handle)

with open(version_path, "r", encoding="utf-8") as handle:
    versioning = json.load(handle)

if notes.get("schema") != 1:
    raise SystemExit("release-notes.json schema must be 1")

tracks = versioning.get("tracks") or {}
releases = notes.get("releases")
if not isinstance(releases, list) or not releases:
    raise SystemExit("release-notes.json must contain at least one release")

semver_re = re.compile(r"^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)$")
ids = set()
matching_target_entries = 0

if ref_type == "tag":
    target_track = "prod"
elif target_track not in tracks:
    raise SystemExit(f"unsupported release track {target_track!r}")

for release in releases:
    release_id = release.get("id")
    if not release_id:
        raise SystemExit("release entry is missing id")
    if release_id in ids:
        raise SystemExit(f"duplicate release id: {release_id}")
    ids.add(release_id)

    track = release.get("track")
    if track not in tracks:
        raise SystemExit(f"{release_id}: unknown track {track!r}")

    version_base = release.get("versionBase")
    if not semver_re.match(str(version_base or "")):
        raise SystemExit(f"{release_id}: versionBase must be numeric SemVer core")

    if version_base != tracks[track].get("base"):
        raise SystemExit(f"{release_id}: versionBase {version_base} does not match {track}.base {tracks[track].get('base')}")

    pattern = release.get("versionPattern")
    expected_pattern = f"{version_base}.<github-run-number>"
    if pattern != expected_pattern:
        raise SystemExit(f"{release_id}: versionPattern must be {expected_pattern}")

    channels = release.get("channels")
    if not isinstance(channels, list) or not channels:
        raise SystemExit(f"{release_id}: channels must be a non-empty list")

    for channel in channels:
        if channel not in release:
            raise SystemExit(f"{release_id}: channel {channel!r} missing matching release section")

    title = str(release.get("title") or "")
    summary = str(release.get("summary") or "")
    if len(title) < 8 or len(summary) < 24:
        raise SystemExit(f"{release_id}: title and summary must be useful for public release notes")

    if track == target_track and version_base == tracks[target_track].get("base") and release.get("public") is True:
        matching_target_entries += 1

if matching_target_entries == 0:
    raise SystemExit(f"release-notes.json needs a public {target_track} entry for base {tracks[target_track].get('base')}")

print(f"Validated {len(releases)} release note entry")
PY
