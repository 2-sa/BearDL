![](resources/banner.png)

This is the inherited Parabolic browser extension bundled with BearDL. It sends
links through the inherited `parabolic://` protocol; the installed application
must be registered to handle it. The Firefox listing below belongs to the upstream
extension. For BearDL application status, see [CURRENT_TASK.md](../docs/CURRENT_TASK.md).

# Features
- **One-Click Capture**: Send the URL of your active tab to Parabolic instantly by clicking the Extension/Add-on icon.
- **Context Menu Integration**: Right-click and select: `Open link in Parabolic` on any specific link to send that exact URL without needing to open the page. Otherwise, use the current tab's URL.
- **Keyboard shortcut**: Press `Alt` + `P` to send the URL of your active tab to Parabolic.
- **Lightweight & Fast**: Designed to be fast, unobtrusive, easy to use, and respects your privacy.

# Installation
> [!NOTE]  
> Use the local installation steps below for Chrome. This repository does not establish a separate BearDL Chrome Web Store listing.
#### Chrome Local Install
1. Go to: `chrome://extensions`.
2. Enable `Developer mode`.
3. Click the `Load unpacked` button and select the extension folder.

##### Firefox
[![get-the-addon](resources/firefox.png)](https://addons.mozilla.org/en-US/firefox/addon/parabolic/)

## Local handoff changes (1.5)

The extension sends the URL to the clicked tab explicitly, even if the active tab
changes while settings load. Chrome and Firefox use Promise-based extension APIs.
Only HTTP(S) pages are forwarded. Browser approval to open an external application
is still required when the browser prompts for it.

The accompanying Windows source change redirects new launches to the running
instance and queues URL dialogs until startup and existing dialogs finish. This
requires an application build containing that change; release 2026.5.147 does not
contain it. Reload an unpacked Chrome extension after updating its folder. Editing
this repository does not update the upstream Firefox store listing.

Run `node --test tests/extension/handoff.test.cjs` for the mocked API checks.
These checks do not replace testing native protocol activation in a browser.