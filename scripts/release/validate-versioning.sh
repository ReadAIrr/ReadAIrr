#!/usr/bin/env bash
set -euo pipefail

config_path="${1:-docs/release-versioning.json}"
track="${READAIRR_RELEASE_TRACK:-${GITHUB_BASE_REF:-${GITHUB_REF_NAME:-dev}}}"
ref_type="${READAIRR_REF_TYPE:-${GITHUB_REF_TYPE:-branch}}"
ref_name="${READAIRR_REF_NAME:-${GITHUB_REF_NAME:-$track}}"

python3 - "$config_path" "$track" "$ref_type" "$ref_name" <<'PY'
import json
import os
import re
import sys

config_path, track, ref_type, ref_name = sys.argv[1:5]

with open(config_path, "r", encoding="utf-8") as handle:
    config = json.load(handle)

if config.get("schema") != 1:
    raise SystemExit("release-versioning.json schema must be 1")

tracks = config.get("tracks") or {}
for required in ("dev", "val", "prod"):
    if required not in tracks:
        raise SystemExit(f"missing release track: {required}")

semver_re = re.compile(r"^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)$")

def parse_base(name):
    base = tracks[name].get("base", "")
    if not semver_re.match(base):
        raise SystemExit(f"{name}.base must be numeric SemVer core, got {base!r}")
    return tuple(int(part) for part in base.split(".")), base

dev_tuple, dev_base = parse_base("dev")
val_tuple, val_base = parse_base("val")
prod_tuple, prod_base = parse_base("prod")

if not (dev_tuple >= val_tuple >= prod_tuple):
    raise SystemExit("release bases must be monotonic: dev >= val >= prod")

if ref_type == "tag":
    if not ref_name.startswith("v"):
        raise SystemExit("release tags must start with v, for example v1.1.0")

    tag_base = ref_name[1:]
    if tag_base != prod_base:
        raise SystemExit(f"tag {ref_name} must match prod.base v{prod_base}")

    selected_track = "prod"
elif track in tracks:
    selected_track = track
else:
    raise SystemExit(f"unsupported release track {track!r}")

if selected_track == "val" and dev_base != val_base:
    raise SystemExit("val promotion requires val.base to equal dev.base")

if selected_track == "prod" and not (dev_base == val_base == prod_base):
    raise SystemExit("prod promotion requires dev.base, val.base, and prod.base to match")

selected_base = tracks[selected_track]["base"]
major, minor, patch = selected_base.split(".")
run_number = os.environ.get("GITHUB_RUN_NUMBER")
full_version = f"{selected_base}.{run_number}" if run_number else f"{selected_base}.0"

outputs = {
    "track": selected_track,
    "base": selected_base,
    "major": major,
    "minor": minor,
    "patch": patch,
    "major_minor": f"{major}.{minor}",
    "version": full_version,
}

github_output = os.environ.get("GITHUB_OUTPUT")
if github_output:
    with open(github_output, "a", encoding="utf-8") as handle:
        for key, value in outputs.items():
            handle.write(f"{key}={value}\n")

print(f"ReadAIrr release track: {selected_track}")
print(f"ReadAIrr version base: {selected_base}")
print(f"ReadAIrr build version: {full_version}")
PY
