# Architecture

## Product in one paragraph

Wordwright lives in the Windows system tray. It watches for snippet triggers (e.g. `;sig`) as you type and replaces them with the stored text, expanding `{date}`, `{time}`, `{clipboard}` and `{cursor}` on the way. It is a text expander and nothing else: it makes no network connections of any kind.

An earlier version also rewrote selected text with an on-device AI model. That is parked, not deleted — see [AI_REWRITING.md](AI_REWRITING.md) and the `ai-rewriting` tag.

## Solution layout

```
Wordwright.slnx
src/
  Wordwright.Core/          Pure .NET class library. No UI, no Win32. Unit-tested.
    Snippets/          Snippet model, store, trigger matcher, variable expander
    Keystrokes/        The typed-character buffer the matcher reads
    Settings/          Settings model + JSON store
    Storage/           Atomic JSON file read/write
  Wordwright.Platform/      Windows-only glue: keyboard hook, clipboard, SendInput,
                       packaged-vs-installer detection, Start with Windows
  Wordwright.App/           WPF app: tray icon, windows, pages, snippet engine,
                       app lifetime, wiring
tests/
  Wordwright.Core.Tests/    xUnit tests for everything in Wordwright.Core
```

WPF-UI allows one `ContentDialogHost` per window and throws when a second one registers. It belongs on `MainWindow`, never on a page: WPF-UI builds a page again every time the user navigates back to it, so a host on a page takes the app down on the second visit.

## Packaging

Two builds ship from the same source. Both keep the user's data in the same place; they differ in how they are installed and how they start with Windows.

- **Installer (GitHub Releases):** `scripts/pack-release.ps1` publishes the app self-contained and packs it with Velopack. `App.xaml.cs` calls `VelopackApp.Build().Run()` in its constructor, the first thing the generated entry point does, so an install or uninstall can finish. The app never checks for updates itself (hard rule 1): new versions are installed by running the new `Setup.exe`, or by the Store for the MSIX package.
- **Microsoft Store package:** `scripts/pack-msix.ps1` packs `packaging/AppxManifest.xml` with the tile assets `scripts/IconGen --msix` renders from the brand geometry. It is a full-trust (`runFullTrust`) desktop package; the manifest's Identity is a placeholder until Partner Center reserves the name.
- Both builds target `net10.0-windows10.0.19041.0`, which is what gives the app the WinRT `StartupTask` API; the manifest declares Windows 10 19045 as the floor.
- **Start with Windows** goes through `Wordwright.Platform.Startup.StartWithWindows`: the `HKCU\...\Run` key when unpackaged, the manifest's `desktop:startupTask` when packaged (Store policy forbids the Run key). `PackageIdentity.IsPackaged` decides which.

## Dependencies (the complete allowed list)

| Package | Used in | Why |
|---|---|---|
| `WPF-UI` (lepoco) | App | Windows 11 Fluent controls, Mica backdrop, light/dark theme |
| `H.NotifyIcon.Wpf` | App | Tray icon and tray menu |
| `CommunityToolkit.Mvvm` | App | MVVM boilerplate |
| `Velopack` | App | Installer and uninstaller (the app never checks for updates) |
| `xunit`, `FluentAssertions` | Tests | Unit tests |

Not packages but bundled assets: Zodiak font files (Fontshare, embedded as WPF resources) and Phosphor icons (MIT) copied as XAML path geometries into `Wordwright.App/Resources/Icons.xaml`. Icons and logo come from `brand/`.

Pin exact versions in `Directory.Packages.props` (central package management).

## Data files

Everything lives under `%AppData%\Wordwright\`.

### `snippets.json`
```json
{
  "schemaVersion": 1,
  "triggerPrefix": ";",
  "snippets": [
    { "id": "b1c2", "trigger": "sig", "name": "Email signature",
      "body": "Best regards,\nJaspreet", "enabled": true,
      "createdUtc": "2026-10-01T10:00:00Z", "updatedUtc": "2026-10-01T10:00:00Z" }
  ]
}
```
- `trigger` is stored without the prefix; unique, case-sensitive, 1–32 chars, letters/digits/`-`/`_` only.
- `body` has no length limit (the editor warns above 100,000 characters, for performance only).
- Writes are atomic: write to `snippets.json.tmp`, then replace. Keep `snippets.json.bak` of the previous version.

## Snippet engine

1. `Wordwright.Platform.KeyboardHook` installs `SetWindowsHookEx(WH_KEYBOARD_LL)` on a dedicated thread with its own message loop. The callback must return in well under 1 ms: enqueue and return; never block.
2. Ignore events with the `LLKHF_INJECTED` flag (our own `SendInput` output) to avoid loops.
3. Translate printable keys with `ToUnicodeEx` using the foreground window's keyboard layout and append to a rolling in-memory buffer (max 64 chars). Backspace removes one char. Enter, Esc, Tab, arrows, Home/End, PgUp/PgDn, mouse clicks and foreground-window changes clear the buffer, and so does a pause of more than 5 s between two typed characters (decision D3), so a trigger typed with a long gap in the middle of it never expands. Nothing happens while the foreground process is in `excludedApps`.
4. After each character, `Wordwright.Core.Snippets.TriggerMatcher` checks whether the buffer ends with `prefix + trigger`, on a word boundary (the char before the prefix is start-of-buffer, whitespace or punctuation). If one trigger is a prefix of another (`;s` and `;sig`), expand only when the next typed char is a space or punctuation (then the space/punctuation is kept after the expansion). Otherwise expand immediately.
5. On a match: send Backspaces for the typed trigger (prefix + trigger length), then paste the expanded body.
6. Variables expanded by `VariableExpander` before paste: `{date}` (system short date), `{time}` (short time), `{clipboard}` (current clipboard text), `{cursor}` (caret ends here: after paste, send Left-arrow presses for the characters after the marker). Unknown `{...}` stay as typed. `{{` produces a literal `{`.

## Paste and selection capture (shared)

- **Paste:** save clipboard (Unicode text, text, RTF, HTML, CSV and file lists, plus a bitmap only when there is no text, within a 200 ms budget) → set clipboard to our text, also adding the `ExcludeClipboardContentFromMonitorProcessing` format so it stays out of Windows clipboard history (Win+V) → `SendInput` Ctrl+V → 400 ms later, without blocking, restore the saved clipboard unless something else has written to it since (clipboard sequence number). A second expansion inside those 400 ms keeps the first one's saved clipboard.
- **Capture selection:** save clipboard → clear → `SendInput` Ctrl+C → poll for new text up to 400 ms → read → restore clipboard. Empty result means "nothing selected".
- **Known limitation:** Windows blocks input from a normal app into apps running as administrator (UIPI). Detect an elevated foreground window and show the message from `UX_COPY.md` instead of failing silently.
## Privacy summary
- **No telemetry, and no network access at all.** There is no code path that opens a connection: no update check, no crash reporting, nothing.
- **No content in logs.** The log file `%AppData%\Wordwright\logs\wordwright-YYYYMMDD.log` (`Core/Diagnostics/EventLog`) records fixed event names only — started, hook refused, hook reinstalled, and an unhandled exception's type, HResult and stack frames, never its message — and day files older than 7 days are deleted at start-up. It sits beside the user's data so that uninstalling or deleting `%AppData%\Wordwright` removes everything. An exception on the UI thread is logged and the app keeps running.
- The keystroke buffer holds at most the last 64 characters, in memory only, and is cleared on focus change, mouse click, Enter, Escape and navigation keys. It is never persisted.
- Windows does not reliably tell other apps when a password field is focused, so `excludedApps` lets users turn Wordwright off in specific programs. The README says this plainly.

## The AI writing assistant (parked)

An earlier Wordwright rewrote selected text on-device with a local model — through `LLamaSharp`, a hardware probe, a model catalog and an evaluation harness. None of that is in this application: the components, the data files (`actions.json`, `models.json`, `installed-models.json`), the prompt, the cleaning rules and the fit rule all live on the **`ai-rewriting`** branch. [AI_REWRITING.md](AI_REWRITING.md) is the full account.
