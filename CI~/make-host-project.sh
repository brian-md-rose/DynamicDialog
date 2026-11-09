#!/usr/bin/env bash
# Writes a throwaway Unity project that loads this package from the repo with its tests enabled.
# CI runs the package's tests and the build smoke test in it.
# The project must be OUTSIDE the package folder: Unity watches a local package's folder for changes, and a project
# inside it floods that watcher with its own Library writes, which made Unity 6 lose script references in Play Mode.
#
# Usage: CI~/make-host-project.sh <unity-version> <full|minimal> [--first-bark] [output-dir]
#   full     uGUI, TextMeshPro, Addressables and Localization installed: every test assembly compiles.
#   minimal  only the Test Framework: checks the package compiles and works without its optional packages.
#   --first-bark  also copies the First Bark sample and the CI build script in (for the player build smoke test).
#   output-dir    defaults to HostProject next to the repo folder (in CI: $GITHUB_WORKSPACE/HostProject).
set -euo pipefail

version="${1:?unity version, for example 6000.5.5f1}"
profile="${2:?full or minimal}"
shift 2
first_bark=false
if [[ "${1:-}" == "--first-bark" ]]; then first_bark=true; shift; fi

ci_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$ci_dir/.." && pwd)"
out="${1:-$(dirname "$repo")/HostProject}"
case "$(cd "$(dirname "$out")" 2>/dev/null && pwd)/" in "$repo"/*) echo "Put the host project outside the package folder ($repo)." >&2; exit 1 ;; esac

case "$version" in
  2021.*)
    test_framework="1.1.33"
    optional='"com.unity.ugui": "1.0.0", "com.unity.textmeshpro": "3.0.6", "com.unity.addressables": "1.21.21", "com.unity.localization": "1.4.5",'
    ;;
  6000.*)
    test_framework="1.7.0"
    optional='"com.unity.ugui": "2.5.0", "com.unity.addressables": "2.9.1", "com.unity.localization": "1.5.14",'
    ;;
  *) echo "No package versions set up for Unity $version (add them to $0)." >&2; exit 1 ;;
esac

case "$profile" in
  full) ;;
  minimal) optional='' ;;
  *) echo "Unknown profile '$profile' (use full or minimal)." >&2; exit 1 ;;
esac
if $first_bark && [[ "$profile" != full ]]; then
  echo "--first-bark needs the full profile (the sample uses uGUI and TextMeshPro)." >&2; exit 1
fi

mkdir -p "$out/Packages" "$out/ProjectSettings" "$out/Assets"

rel="$(realpath --relative-to="$out/Packages" "$repo" 2>/dev/null || echo "$repo")"

cat > "$out/Packages/manifest.json" <<JSON
{
  "dependencies": {
    "com.dynamicdialog": "file:$rel",
    $optional
    "com.unity.test-framework": "$test_framework"
  },
  "testables": ["com.dynamicdialog"]
}
JSON

# A ProjectVersion.txt marks this as an existing project, so Unity doesn't add its default packages to the manifest.
printf 'm_EditorVersion: %s\n' "$version" > "$out/ProjectSettings/ProjectVersion.txt"

if $first_bark; then
  mkdir -p "$out/Assets/FirstBark" "$out/Assets/Editor"
  cp -R "$repo/Samples~/FirstBark/." "$out/Assets/FirstBark/"
  cp "$ci_dir/Build/DynamicDialogCiBuild.cs" "$out/Assets/Editor/"
fi

echo "Host project for Unity $version ($profile) in $out. Manifest:"
cat "$out/Packages/manifest.json"

# The presentation tests and First Bark need TextMeshPro's Essential Resources. Set TMP_ESSENTIALS_PACKAGE to a
# local "TMP Essential Resources.unitypackage" to skip downloading it.
if [[ "$profile" == full ]]; then
  bash "$ci_dir/import-tmp-essentials.sh" "$version" "$out" "${TMP_ESSENTIALS_PACKAGE:-}"
fi
