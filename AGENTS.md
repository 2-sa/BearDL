# Repository Guidelines

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

Commands run from the repository root:

- `git diff --check`: local whitespace check.
- `dotnet build Nickvision.Parabolic.Shared`: compile shared code on a configured runner.
- `dotnet publish Nickvision.Parabolic.WinUI -c Release`: release publishing; use the workflow's runtime, platform, and restore arguments.

Keep SDK and gettext setup in CI. Builds update translation catalogs; review generated changes before committing.

## Coding Style & Naming Conventions

Use four-space C# indentation, separate-line braces, file-scoped namespaces, explicit imports, and nullable annotations. Use `PascalCase` for types/methods/properties, `camelCase` for locals, `_camelCase` for instance fields, and `s_` for static fields. Preserve existing XML indentation. CI uses codespell; no dedicated formatter configuration exists.

## Arabic & RTL Guidelines

Apply the `arabic-ui` skill to Arabic interface work. Derive direction from the active UI language using native toolkit direction support. Preserve English LTR layouts. Place Arabic navigation on the right and review alignment, spacing, focus order, and popup placement. Mirror directional arrows only, preserving nondirectional icons and artwork.

Isolate URLs, paths, and numeric expressions as LTR where needed without reversing stored text. Use concise Arabic and gettext plural forms.

Keep toggle labels and switches in one horizontal row: Arabic labels on the right, switches on the left. Reserve a separate column for the switch; allow long labels to wrap within their own column. Disable responsive stacking for toggle settings cards. Preserve accessible labels and dependent settings.

Keep vertical scrollbars on the right in every window. Give scrolling containers LTR direction and reserve scrollbar space; explicitly restore the UI language direction on their content.

## Testing Guidelines

No automated test suite or coverage threshold is configured. Build affected platforms on GitHub Actions. Verify downloaded artifacts for Arabic RTL, English LTR, mixed text, dialogs, menus, keyboard navigation, and downloads. Report CI results separately from visual verification; capture screenshots before claiming visual completion.

## Commit & Pull Request Guidelines

History mixes descriptive subjects with `fix:` and `docs(readme):`. Keep commits focused. PRs should include behavior changes, relevant issues, validation results, and UI screenshots. Follow `CONTRIBUTING.md` for translation and private security reporting.
