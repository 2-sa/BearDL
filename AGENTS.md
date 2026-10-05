# Repository Guidelines

## Start Here & Current Authorization

Read [docs/CURRENT_TASK.md](docs/CURRENT_TASK.md) before making changes, and
[docs/RELEASING.md](docs/RELEASING.md) before building or delivering an update.
Verify the current Git status, branch, remote, and workflow state rather than
assuming the handoff snapshot is still current. Preserve pending local changes.

The user's latest instruction is to keep changes local and **not make a commit
until explicitly asked**. Do not commit, push, or start a new distributable build
without that authorization. When the user later authorizes delivery, target
`origin` (`2-sa/BearDL`) and `main` unless they explicitly choose another target.
Update the current-task document after meaningful progress so a new conversation
can continue without reconstructing the previous chat.

## Repository Identity & Goal

BearDL ([2-sa/BearDL](https://github.com/2-sa/BearDL), `origin`) forks [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic). Target contributions to BearDL unless explicitly contributing upstream. Existing `Nickvision.Parabolic.*` names are inherited.

The fork's primary goal is complete Arabic RTL support across navigation, menus, text, toolbars, dialogs, settings, and download rows. Translation alone does not satisfy this goal.

## Project Structure & Module Organization

This .NET 10 yt-dlp frontend follows MVC:

- `Nickvision.Parabolic.Shared/`: shared controllers, models, events, and services; keep business logic here.
- `Nickvision.Parabolic.WinUI/`: Windows views, XAML controls, and assets.
- `Nickvision.Parabolic.GNOME/`: GNOME views, Blueprint layouts, and resources.
- `extension/`: browser extension.
- `resources/`: artwork, gettext catalogs (`po/`), and yt-dlp plugins.
- `flatpak/`, `inno/`, `.github/workflows/`: packaging and CI.

## Build, Test, and Development Commands

Prefer GitHub Actions for dependency installation, builds, automated checks, and packaging. Keep the local machine focused on editing, source inspection, and lightweight Git checks; do not install build toolchains or run local builds unless the user requests it. Use the fork's `.github/workflows/windows.yml` for Windows builds and deliver its artifacts for visual testing.

Ask first and wait for explicit permission before downloading or installing any
program, SDK, runtime, build tool, or development certificate on the user's
device. A request to build or update the app does not authorize installing tools.
Keep toolchain installation and certificate setup on GitHub runners.

Commands run from the repository root:

- `git diff --check`: local whitespace check.
- `dotnet build Nickvision.Parabolic.Shared`: compile shared code on a configured runner.
- `dotnet publish Nickvision.Parabolic.WinUI -c Release`: release publishing; use the workflow's runtime, platform, and restore arguments.

Keep SDK and gettext setup in CI. Builds update translation catalogs; review generated changes before committing.

## Build & Release Numbering

Every new distributable build must receive a new, increasing version number. Use `.github/workflows/release.yml`, which generates the version from `github.run_number` and passes it to every platform, installer, and release tag. Never reuse a previous release number for changed code. Start a new workflow run for a new build; reserve reruns for retrying failed jobs from the same build. Platform-only workflows with the fallback version `2026.5.0` are validation builds, not distributable releases.

## Coding Style & Naming Conventions

Use four-space C# indentation, separate-line braces, file-scoped namespaces, explicit imports, and nullable annotations. Use `PascalCase` for types/methods/properties, `camelCase` for locals, `_camelCase` for instance fields, and `s_` for static fields. Preserve existing XML indentation. CI uses codespell; no dedicated formatter configuration exists.

## Arabic & RTL Guidelines

Apply the `arabic-ui` skill to Arabic interface work. Derive direction from the active UI language using native toolkit direction support. Preserve English LTR layouts. Place Arabic navigation on the right and review alignment, spacing, focus order, and popup placement. Mirror directional arrows only, preserving nondirectional icons and artwork.

Isolate URLs, paths, and numeric expressions as LTR where needed without reversing stored text. Use concise Arabic and gettext plural forms.

Keep Arabic field labels in the language-directed container; give LTR direction
only to the URL, path, username, password, or argument input. Use separate labels
with `AutomationProperties.LabeledBy` when an LTR input would move its header to
the wrong side. `LocalizationHelper.CreateLabeledInput` supports dynamic forms.

Keep toggle labels and switches in one horizontal row: Arabic labels on the right, switches on the left. Reserve a separate column for the switch; allow long labels to wrap within their own column. Disable responsive stacking for toggle settings cards. Preserve accessible labels and dependent settings.

Keep vertical scrollbars on the right in every window. Give scrolling containers LTR direction and reserve scrollbar space; explicitly restore the UI language direction on their content.

Constrain the content width as well as the scroll viewport when direction changes
inside a scrolling container. Use `ScrollViewportHelper.ConstrainContentWidth`
to account for padding and content margins as the viewport changes. Avoid fixed
500-DIP list cards and negative margins that make RTL text or icons extend beyond
the visible area. Stretch list item content within the available width.

Keep the initial URL dialog and media-discovery progress dialog compact. Quality,
subtitle, and advanced pages share a fixed configuration viewport so selecting a
tab cannot shrink or enlarge the dialog. Empty title-bar space must remain
draggable; menus and caption buttons must retain their own input regions.

## Testing Guidelines

Subtitle plugin regression tests live in `tests/plugins/`; `.github/workflows/plugins.yml` checks bundled and latest yt-dlp before release builds. No .NET test suite or coverage threshold is configured. Build affected platforms on GitHub Actions. Verify downloaded artifacts for Arabic RTL, English LTR, mixed text, dialogs, menus, keyboard navigation, and downloads. Report CI results separately from visual verification; capture screenshots before claiming visual completion.

## Commit & Pull Request Guidelines

History mixes descriptive subjects with `fix:` and `docs(readme):`. Keep commits focused. PRs should include behavior changes, relevant issues, validation results, and UI screenshots. Follow `CONTRIBUTING.md` for translation and private security reporting.
