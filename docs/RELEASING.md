# BearDL automatic releases

Read [CURRENT_TASK.md](CURRENT_TASK.md) first. The user currently requires all new
edits to stay local until explicitly asking for a commit. Do not interpret this
release guide as authorization to commit, push, install tools, or start a build.
When delivery is authorized, use `2-sa/BearDL` and the user's chosen `main` branch.
Keep SDKs, build tools, dependencies, and certificate setup on GitHub runners.
Ask and wait for explicit permission before downloading or installing software
on the user's device; a build request does not grant that permission.

`.github/workflows/release.yml` builds and publishes every push to `main`.
Run **Build and release BearDL** manually to build the selected branch as well.
Pull request checks and standalone platform workflows produce artifacts only.

## Windows download before release publication

Windows jobs upload `BearDLSetup-x64` and `BearDLSetup-arm64` artifacts as soon as
their packaging completes. They can be downloaded while Linux or macOS jobs are
still running; there is no need to wait for the final release job.

Get the artifact IDs with:

```powershell
gh api repos/2-sa/BearDL/actions/runs/RUN_ID/artifacts
```

Give the verified link
`https://github.com/2-sa/BearDL/actions/runs/RUN_ID/artifacts/ARTIFACT_ID`.
Explain that this downloads a ZIP containing the EXE, rather than presenting it
as a direct EXE link. For a published release, use the verified tag-specific
`https://github.com/2-sa/BearDL/releases/download/VERSION/BearDLSetup-x64.exe`
link. Check that it belongs to the commit containing the requested fix; the
`latest` URL can still point to an older build until publication finishes.

## Version and publication

The release workflow assigns `2026.5.(100 + run_number)` to all six builds.
Rerunning a workflow keeps its version; a new workflow run gets a newer version.
MSBuild propagates it to application assemblies; installers and macOS bundle
metadata receive the same value. The application reads its assembly version.

Publication runs only when all Windows, macOS, and Linux builds succeed. The
workflow creates a numeric tag pointing to the built commit and publishes these
assets together:

- `BearDLSetup-x64.exe`, `BearDLSetup-arm64.exe`
- `BearDLPortable-x64.zip`, `BearDLPortable-arm64.zip`
- `BearDL-macOS-x64.zip`, `BearDL-macOS-arm64.zip`
- `BearDL-linux-x86_64.flatpak`, `BearDL-linux-aarch64.flatpak`
- `SHA256SUMS.txt`

The Windows updater checks published releases in `2-sa/BearDL`, selects the
matching `setup-x64.exe` or `setup-arm64.exe` suffix, and checks the GitHub asset
digest. Keep installer filenames stable. Dependency updates still use their own
upstream projects. Draft releases and Actions artifacts are not application updates.

## Platform verification

Windows bundles include runtime and media tools. macOS ZIPs preserve executable
permissions and contain the app and dependencies; builds target macOS 15 or later.
The macOS app is ad-hoc signed, not Apple-notarized. Flatpak requires its referenced
GNOME runtime. CI success does not establish visual or download correctness.

Before announcing a release as visually verified, test Arabic RTL, English LTR,
caption controls, scrolling, normal downloads, Fast Download, playlists, and
updating from an older Windows release. Fixes are published by the next successful
run; do not move an existing version tag to different source code.
