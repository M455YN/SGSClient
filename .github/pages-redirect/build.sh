#!/usr/bin/env bash
# Turns a built docs site into redirect stubs for GitHub Pages: every old page points to the same
# path on sgs.m455yn.io/docs/, and 404.html catches anything else. Usage: build.sh <site dir> <out dir>
set -euo pipefail
site="$1"; out="$2"; here="$(cd "$(dirname "$0")" && pwd)"
base="https://sgs.m455yn.io/docs/"

rm -rf "$out"; mkdir -p "$out"
count=0
while IFS= read -r f; do
  dir="${f#"$site"/}"; dir="${dir%index.html}"   # "" for the root, "creators/" for a section
  mkdir -p "$out/$dir"
  sed "s|__TARGET__|${base}${dir}|g" "$here/page.html" > "$out/${dir}index.html"
  count=$((count + 1))
done < <(find "$site" -name index.html | sort)
cp "$here/404.html" "$out/404.html"
echo "redirect stubs: $count"
