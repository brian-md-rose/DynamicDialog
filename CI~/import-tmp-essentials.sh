#!/usr/bin/env bash
# Unpacks TextMeshPro's Essential Resources (default font, TMP Settings, shaders) into a Unity project before Unity
# opens it. The presentation tests and the First Bark sample need them, and importing them from inside a test run
# happens asynchronously on Unity 6, after the tests have already started.
#
# Usage: CI~/import-tmp-essentials.sh <unity-version> <project-dir> [path/to/TMP Essential Resources.unitypackage]
# Without a .unitypackage it fetches one:
#   Unity 6     uGUI 2.x (which contains TextMeshPro) ships inside the editor, so it is copied out of GameCI's editor
#               Docker image (the same image the tests run in; no license needed to read a file from it).
#   2021.3      from the com.unity.textmeshpro package on Unity's package registry.
set -euo pipefail

version="${1:?unity version}"
project="${2:?project dir}"
package="${3:-}"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ -z "$package" ]]; then
  package="$work/essentials.unitypackage"
  case "$version" in
    6000.*)
      image="unityci/editor:ubuntu-$version-base-3"
      docker run --rm --entrypoint sh "$image" -c \
        'f=$(find / -xdev -path "*com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage" 2>/dev/null | head -n 1); [ -n "$f" ] && cat "$f"' \
        > "$package"
      ;;
    2021.*)
      curl -fsSL "https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-3.0.6.tgz" | tar -xz -C "$work"
      cp "$work/package/Package Resources/TMP Essential Resources.unitypackage" "$package"
      ;;
    *) echo "Don't know where to get TMP Essential Resources for Unity $version." >&2; exit 1 ;;
  esac
fi
[[ -s "$package" ]] || { echo "Couldn't get TMP Essential Resources for Unity $version." >&2; exit 1; }

# A .unitypackage is a gzipped tar with one folder per asset GUID: pathname (the asset path), asset (the file;
# absent for folders) and asset.meta.
mkdir -p "$work/unpacked"
tar -xzf "$package" -C "$work/unpacked"
count=0
for entry in "$work/unpacked"/*/; do
  [[ -f "$entry/pathname" ]] || continue
  path="$(head -n 1 "$entry/pathname" | tr -d '\r')"
  dest="$project/$path"
  if [[ -f "$entry/asset" ]]; then
    mkdir -p "$(dirname "$dest")"
    cp "$entry/asset" "$dest"
    count=$((count + 1))
  else
    mkdir -p "$dest"
  fi
  if [[ -f "$entry/asset.meta" ]]; then cp "$entry/asset.meta" "$dest.meta"; fi
done
echo "Unpacked $count TMP Essential Resources files into $project/Assets."
