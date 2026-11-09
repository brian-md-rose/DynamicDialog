# Continuous integration

GitHub Actions workflows live in `.github/workflows`. This folder holds what they run. Unity ignores folders whose
names end in `~`, so none of this is imported with the package.

| Workflow | Job | Runs | Needs a Unity license |
|---|---|---|---|
| `ci.yml` | **.NET tests**: `dotnet test Tests~/DotNet/Tests` (the core tests in `Tests/Editor`), plus the `netstandard2.1` warnings-as-errors build of `Runtime/Core` | every PR and push to `main` | no |
| `ci.yml` | **Lint workflows**: actionlint and shellcheck | every PR and push to `main` | no |
| `ci.yml` | **Unity tests**: Edit Mode and Play Mode tests, 4 legs (below) | every PR and push to `main`; skipped with a warning when the license secrets aren't set | yes |
| `unity-build.yml` | **Build smoke test**: builds First Bark as a Linux player (IL2CPP by default), runs it headless for 20 s and fails on any exception | by hand: Actions > *Unity build smoke test* > Run workflow | yes |

Unity test legs (GameCI `unity-test-runner`, Linux editor images):

| Unity | `full`: uGUI + TextMeshPro, Addressables, Localization | `minimal`: Test Framework only |
|---|---|---|
| 2021.3.45f2 | every test assembly | checks the optional assemblies drop out cleanly |
| 6000.5.5f1 | every test assembly | checks the optional assemblies drop out cleanly |

Test results (NUnit XML) are uploaded as artifacts on every run, and each Unity leg also posts a check with the results.
Each leg caches its host project's `Library` folder.

## The host project

A package can't be tested on its own, so `make-host-project.sh` writes a throwaway project that loads the package from
the repo (a relative `file:` path) with `"testables": ["com.dynamicdialog"]`. It is generated rather than
committed because the manifest differs per Unity version and per leg (uGUI 2.x on Unity 6, uGUI 1.0 plus TextMeshPro 3.0
on 2021.3; with or without the optional packages), and a generated project can't drift out of date. A
`ProjectVersion.txt` is all it needs; Unity creates the rest of `ProjectSettings` and, because the project isn't
"new", doesn't add its default packages to the manifest.

The project has to sit **outside** the package folder. Unity watches a local package's folder for changes, and a project
inside it floods that watcher with its own `Library` writes. On Unity 6 that made Play Mode lose script references (the
prefabs' TextMeshPro components, even the test runner's own scene) or hang on entering Play Mode. So CI checks the repo
out into `package/` and generates `HostProject/` beside it, and the script refuses an output folder inside the repo. By
default it writes `HostProject` next to the repo folder.

The `full` profile also unpacks TextMeshPro's Essential Resources into `Assets/` (`import-tmp-essentials.sh`), because
the presentation tests and First Bark need them. That happens before Unity starts: importing them from inside a test
run finishes asynchronously on Unity 6, after the tests have started. On Unity 6 the `.unitypackage` comes out of the
GameCI editor image (uGUI ships inside the editor); on 2021.3 it comes from the `com.unity.textmeshpro` package on
Unity's registry.

Run it locally the same way CI does, for example:

```bash
CI~/make-host-project.sh 6000.5.5f1 full ../HostProject
"<Unity 6000.5.5f1>/Editor/Unity.exe" -batchmode -nographics -projectPath ../HostProject -runTests -testPlatform PlayMode -testResults results.xml
```

Locally, set `TMP_ESSENTIALS_PACKAGE` to the editor's own copy (for example
`<Editor>/Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage`)
instead of pulling the Docker image.

`Build/DynamicDialogCiBuild.cs` is the build smoke test's build method. `make-host-project.sh --first-bark` copies it and
the First Bark sample into the host project.

## Secrets

The Unity jobs need these repository secrets (GitHub: **Settings > Secrets and variables > Actions > New repository
secret**). The .NET and lint jobs need none.

| Secret | Personal license | Plus or Pro license |
|---|---|---|
| `UNITY_EMAIL` | your Unity ID email | your Unity ID email |
| `UNITY_PASSWORD` | your Unity ID password | your Unity ID password |
| `UNITY_LICENSE` | the contents of your `Unity_lic.ulf` file | not needed |
| `UNITY_SERIAL` | not needed | your serial number |

### Personal license

1. On your own machine, open Unity Hub and sign in. Under **Preferences > Licenses**, make sure you have an active
   Personal license (**Add > Get a free personal license** if not).
2. Find the license file Hub wrote:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. Add the three secrets. With the GitHub CLI, from the repo folder:

   ```bash
   gh secret set UNITY_LICENSE < "C:\ProgramData\Unity\Unity_lic.ulf"
   ```

   ```bash
   gh secret set UNITY_EMAIL
   ```

   ```bash
   gh secret set UNITY_PASSWORD
   ```

   (Each `gh secret set` without a file prompts for the value, so it doesn't end up in your shell history.) Or paste
   them into the web form; for `UNITY_LICENSE`, paste the whole file, XML and all.
4. Re-run the CI workflow. The **Unity license check** job reports whether the secrets are set.

A Personal license file can stop working when it is renewed or returned in Hub; if the Unity jobs start failing at
activation, repeat steps 2 and 3. GameCI's activation guide is at https://game.ci/docs/github/activation.

### Plus or Pro license

Set `UNITY_SERIAL` (your subscription's serial number, shown in your Unity ID account at https://id.unity.com), `UNITY_EMAIL`
and `UNITY_PASSWORD`. GameCI activates a seat for each job and returns it when the job ends.

## Limits

The repo is private, so GitHub-hosted runner minutes count against the account's quota. A cold Unity leg (no `Library`
cache) spends most of its time importing; later runs restore the cache. To save minutes, drop the `minimal` legs from
the matrix in `ci.yml`, or change its `on:` to run the Unity job only on pushes to `main`. The build smoke test only
runs when started by hand.
