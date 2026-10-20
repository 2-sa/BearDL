# BearDL Repository Guidelines

## Start here

This is the inner Git repository. When starting from the outer workspace, enter `BearDL/` before Git commands. Use repository-relative paths in documentation; do not include personal device paths.

Read these documents in order:

1. [docs/CURRENT_TASK.md](docs/CURRENT_TASK.md): live task, authorization, pending problems, and continuation steps.
2. [docs/RELEASING.md](docs/RELEASING.md): build and delivery workflow.
3. [docs/PROJECT_AND_COMMIT_GUIDE.md](docs/PROJECT_AND_COMMIT_GUIDE.md): architecture and the 22 historical commits.
4. [docs/RESOURCE_REVIEW_2026-10-14.md](docs/RESOURCE_REVIEW_2026-10-14.md): resource review, measurements, and their limits.

Inspect `git status --short`, `git diff`, branch, remotes, and relevant CI results. Preserve local edits and new files. Treat dated history as evidence, not as the current task or reusable permission. Follow the latest direct user instruction; summarize the current state and unresolved work before editing.

## Authorization and delivery

Keep changes local until the user explicitly authorizes a commit or delivery. Do not commit, push, or start a distributable build on your own. A previous authorized delivery does not authorize unrelated later changes. When delivery is authorized, use `origin` (`2-sa/BearDL`) and `main` unless the user chooses another target.

Ask and wait before downloading or installing software, SDKs, runtimes, build tools, or development certificates on the user's device. A build request does not grant installation permission. Prefer existing tools for lightweight checks and GitHub Actions for dependency setup, application builds, and packaging. Do not run local application builds unless requested.

`.github/workflows/release.yml` starts only by explicit manual dispatch; a push to `main` runs focused checks, not release packaging. Confirm the built commit and artifacts before sharing download links. New distributable code needs a new increasing version, assigned by the release workflow as `2026.5.(100 + github.run_number)`. Reruns retry the same code and version; do not reuse an old version for changed code. Standalone platform workflows with fallback `2026.5.0` are validation builds.

## Repository identity and architecture

[BearDL](https://github.com/2-sa/BearDL) forks [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic). Target this fork by default and retain upstream attribution. Existing `Nickvision.Parabolic.*` and application identifiers are inherited; renaming them can affect resources, storage, launch protocols, and packaging.

The fork aims for native Arabic wording and complete RTL behavior while preserving English layouts. Translation alone does not satisfy that goal.

- `Nickvision.Parabolic.Shared/`: controllers, services, models, events, configuration extensions, and shared business logic.
- `Nickvision.Parabolic.WinUI/`: Windows XAML, controls, and views.
- `Nickvision.Parabolic.GNOME/`: GTK/libadwaita C# views and `Blueprints/` for both Linux and macOS.
- `resources/po/`: gettext translations; `resources/yt-dlp-plugins/`: bundled extractors.
- `extension/`: browser integration using the inherited launch protocol.
- `.github/workflows/`, `flatpak/`, `inno/`, `resources/macos/`, `resources/linux/`: CI and packaging.
- `tests/`: regression checks; `docs/`: role-specific guides.

The solution is `Nickvision.Parabolic.slnx`, using .NET 10. Keep business behavior in Shared and implement UI changes in both frontends when requested for all platforms. WinUI changes do not propagate to GTK automatically. Check existing features before proposing additions.

## Coding and resources

Use four-space C# indentation, separate-line braces, file-scoped namespaces, explicit imports, and nullable annotations. Use `PascalCase` for types, methods, and properties; `camelCase` for locals; `_camelCase` for instance fields; `s_` for static fields. Preserve established XML formatting. CI uses codespell.

Use the UI dispatcher when modifying WinUI controls and the existing GLib dispatch path for GTK. Only one WinUI ContentDialog may be open at a time; handle actual closure and shutdown when queuing confirmations. Qualify types or use aliases when imported namespaces expose the same name, such as the two `DispatcherQueueTimer` types.

Avoid unnecessary polling, per-layout allocations, full-history scans for a single URL, and unbounded background extraction. Keep waits asynchronous, release gates in `finally`, stop timers when their owner closes, and detach handlers that retain message data. Compare resource samples with identical inputs and distinguish UI overhead from yt-dlp/FFmpeg work.

## Arabic and RTL

Apply the `arabic-ui` skill to Arabic UI and documentation. Use concise native phrasing, gettext plural forms, and the active UI language to determine direction. Keep English LTR intact. Mirror directional arrows only, not artwork or nondirectional icons.

Keep Arabic labels in language-directed containers. Isolate URL, path, username, password, argument, and numeric text as LTR where needed without changing stored values. Use separate labels and `AutomationProperties.LabeledBy`; `LocalizationHelper.CreateLabeledInput` supports dynamic fields.

Keep toggle labels and switches in one row: Arabic label right, switch left. Reserve a separate column for the switch, allow label wrapping, and disable responsive stacking for toggle settings cards. Apply `ToggleSwitchHelper.Apply` directly to new named WinUI toggles after localization, including hidden pages. Preserve `IsOn`, accessibility, and dependent options. The required Arabic knob direction is off on the right and on on the left.

Keep vertical scrollbars on the right: scrolling container LTR, content restored to the UI direction. Use `ScrollViewportHelper.ConstrainContentWidth` to account for padding and margins. Avoid fixed 500-DIP cards and negative margins; stretch list contents within the viewport.

Keep the URL and discovery dialogs compact. Configuration tabs share one fixed viewport. Empty title-bar space must remain draggable; menus and caption buttons must retain correct hit regions after layout and DPI changes.

Keep Arabic `translation-credits` empty as requested. Preserve other project credits, license files, and the attribution on the pinned Inno Setup translation. Review generated PO changes without restoring removed credits.

## Validation

Run `git diff --check` locally and inspect affected source, XAML, Blueprint, and translation keys with gettext contexts. Do not call XML parsing a successful XAML build.

`.github/workflows/plugins.yml` runs these checks on configured runners before release builds:

- `python -m unittest discover -s tests/plugins -v`: subtitle extraction against bundled and latest yt-dlp.
- `dotnet run --project tests/duplicates/DuplicateDownloadChecks.csproj --configuration Release`: duplicate detection and bounded allocation/file-check scenarios.

These are focused checks, not a comprehensive .NET/UI test suite or a guarantee about whole-app CPU and memory. Build every affected platform on Actions. Build steps can update translation catalogs; review generated changes before committing.

Verify artifacts for Arabic RTL, English LTR, mixed technical text, toggle states, scrolling, title-bar dragging, dialogs, keyboard navigation, normal/fast/batch/playlist downloads, and Windows updating. Resource benchmarks, compilation, release publication, installed behavior, and visual acceptance are separate results. Capture screenshots before claiming visual completion. The read-only `tests/windows/Verify-DownloadDialogLayout.ps1` supplements visual checks.

## Documentation and review

Keep `CURRENT_TASK.md` as the single current-state and handoff document. Keep architecture/history in `PROJECT_AND_COMMIT_GUIDE.md`, build rules in `RELEASING.md`, and measurements in the resource report. Do not recreate a parallel dated handoff with conflicting authorization or stale build status.

After meaningful progress, update the current-state document with what changed, what was tested, known failures, and the next step. Do not start another release just to record build status unless requested.

Follow [CONTRIBUTING.md](CONTRIBUTING.md) for contribution, translation, and private security reporting. Keep commits focused. PR descriptions should state the final behavior, affected platforms, validation, and any unverified runtime or visual result; attach screenshots for UI changes.
