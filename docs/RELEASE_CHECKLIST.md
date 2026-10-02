# Release checklist

Every check that gates a release, in one place (docs/PLAN.md P13.9). It replaces the human-check list in PLAN.md's *Current status*, PLAN.md's old *Release checklist* section and the P12 human checks, and it carries the DESIGN.md polish checklist.

**How to use it.** For each release, copy the table's *Result*, *Date* and *Build* columns into a new block under [Runs](#runs) at the bottom, or fill them in here for the release in progress. *Result* is **Pass**, **Fail** (with a line saying what happened) or **n/a** (with why). *Build* is the commit hash or the installer's version. A release ships when every **Gate** row passes; **Polish** rows may ship with a written reason.

Rows marked *machine* need the maintainer's real Windows PC (typing on a real keyboard: Wordwright's hook ignores injected keys, so no script can stand in). Rows marked *script* can be run by anyone from the repository.

## v1.0.0 (in progress)

### Build and package

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| B1 | Release build has 0 warnings, 0 errors | `dotnet build -c Release` | Gate, script | Pass | 2026-10-03 | `0e91590` |
| B2 | Core tests pass | `dotnet test -c Release` (192 today) | Gate, script | Pass (192/192) | 2026-10-03 | `0e91590` |
| B3 | CI is green, including the harness builds and both packaging scripts | GitHub Actions on the release commit | Gate, script | | | |
| B4 | Paste harness passes | `dotnet run --project scripts/PasteHarness` | Gate, script | Pass (12 checks) | 2026-10-03 | `04ce056` |
| B5 | Hook probe passes, US and German layouts | `dotnet run --project scripts/HookProbe` | Gate, script | | | |
| B6 | Installer, portable zip and MSIX build | `scripts/pack-release.ps1`, `scripts/pack-msix.ps1` | Gate, script | Pass (Setup 85.6 MB, MSIX 74.9 MB) | 2026-10-03 | `9d42dd1` |
| B7 | A fresh clone builds: the Zodiak fonts are fetched and verified | delete `Resources/Fonts/*.otf`, build | Gate, script | Pass | 2026-10-03 | `0e91590` |
| B8 | No Zodiak `.otf` anywhere in the public history or tags | `git log --all -- '*.otf'` is empty | Gate, script | **Fail** (purge pending, P13.12) | 2026-10-03 | `0e91590` |

### Install and data

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| I1 | Installer on a fresh user account: create, edit and delete snippets; all persist after a restart | Setup.exe, new Windows account | Gate, machine | | | |
| I2 | Install and uninstall leave nothing outside `%AppData%\Wordwright` | compare `%LocalAppData%`, `%AppData%`, `HKCU\Software` before and after | Gate, machine | | | |
| I3 | The editor never loses an edit: type, then at once switch snippet, change page, or quit from the tray | Snippets page | Gate, machine | | | |
| I4 | MSIX on the same account: repeat I1, and Start with Windows works through the packaged startup task | `pack-msix.ps1 -Certificate`, install, sign out and in | Gate, machine | | | |
| I5 | Store build data: Open data folder shows the real `%AppData%\Wordwright`; the Start with Windows toggle matches Task Manager → Startup apps after turning it off there | MSIX build | Gate, machine | | | |
| I6 | Portable zip does not add itself to Start with Windows | unzip, run, check `HKCU\...\Run` | Gate, machine | | | |
| I7 | Export to a folder you cannot write (e.g. `C:\Windows`) says so and keeps the app running | Settings → Export | Polish, machine | | | |

### Expansion

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| E1 | `;sig` and `;date` expand in Notepad, Word, Outlook, Chrome (Gmail), Slack or Teams, VS Code and the Windows search box | type by hand | Gate, machine | | | |
| E2 | The clipboard is the same afterwards, and nothing from Wordwright appears in Win+V history | copy something first, expand, Win+V | Gate, machine | | | |
| E3 | A 10,000-character snippet inserts in under 1 s; typing never lags | stopwatch | Gate, machine | | | |
| E4 | Typing delay: `;si`, wait six seconds, `g` → nothing; `;sig` at normal speed → expands | type by hand | Gate, machine | | | |
| E5 | Welcome window: `;date` in the try-it box expands in place and the label switches to the done text | first run | Gate, machine | | | |
| E6 | Heavy clipboard: copy a large Excel range, type `;sig` (time it), then paste the range again | Excel | Gate, machine | | | |
| E7 | Teams over Remote Desktop: the snippet arrives before the clipboard comes back | RDP session | Polish, machine | | | |
| E8 | Excluded app: no expansion in an app on the excluded list | Settings → Excluded apps | Gate, machine | | | |

### Keyboard layouts and input

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| K1 | German QWERTZ, Spanish, French AZERTY and UK: `;date` with the default prefix | switch layout, type | Gate, machine | | | |
| K2 | Same layouts with a Shift prefix such as `:`, with Caps Lock on, and with an AltGr character in a snippet | as K1 | Gate, machine | | | |

### Screens and look

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| S1 | Light, dark and a Windows high-contrast theme: every page readable; in high contrast the selected row, the shortcut chip and caution text use the system colours | Settings → Theme; Windows contrast themes | Gate, machine | | | |
| S2 | 100 %, 150 %, 200 % and 300 % scaling; 800×600 and maximised | `scripts/check-screens.ps1` for captures | Gate, machine | | | |
| S3 | Two monitors with different scaling: drag the window across, it stays sharp; a window closed on monitor 2 reopens there | two screens | Gate, machine | | | |
| S4 | A 1366×768 display at 125 %: the window fits | display settings | Gate, machine | | | |
| S5 | Resize below 800×600 is refused; close and reopen, the window is where it was | | Gate, machine | | | |
| S6 | Hovering Maximise shows the Snap Layouts flyout, on the main and the welcome window | Windows 11 | Polish, machine | | | |
| S7 | Tray menu matches the taskbar theme, light and dark; on Windows 11 22H2+ it is on Acrylic with rounded corners | right-click the tray icon | Gate, machine | | | |
| S7a | Tray glyph dims to half strength when Snippets is switched off in the tray menu, and comes back when it is switched on; same on a light and a dark taskbar (D19) | right-click the tray icon, untick Snippets on | Polish, machine | | | |
| S8 | Theme setting re-skins the window at once and survives a restart | Settings → Theme | Gate, machine | | | |
| S9 | "Show animations in Windows" off: no motion at all, nothing else changes | Windows → Accessibility → Visual effects | Gate, machine | | | |
| S10 | DESIGN.md polish list: hierarchy and spacing, type scale, visible focus, empty states, no placeholder text, anti-slop list | read every page | Polish, machine | | | |

### Accessibility

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| A1 | Keyboard-only use of every screen; Ctrl+N, Ctrl+F and Delete work | no mouse | Gate, machine | | | |
| A2 | Zero unnamed controls | `scripts/check-screens.ps1`, then Accessibility Insights for Windows | Gate, machine | | | |
| A3 | Narrator reads the snippet list and the editor | Narrator | Gate, machine | | | |

### Windows 10

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| W1 | Windows 10 22H2 (19045) in a VM: installs; Mica falls back to a plain background (not black or transparent); Segoe UI fallback reads fine; tray icon and the solid tray menu; `;date` expands | VM | Gate, machine | | | |

### Robustness and privacy

| # | Check | How | Kind | Result | Date | Build |
|---|---|---|---|---|---|---|
| R1 | Long session: 8 hours running, one sleep/resume and one lock/unlock, then `;date` still expands | leave it running | Gate, machine | | | |
| R2 | No network connections at any time | Resource Monitor → Network, during R1 | Gate, machine | | | |
| R3 | The log holds event names only, never typed text, clipboard or snippet text | read `%AppData%\Wordwright\logs` after E1–E6 | Gate, machine | | | |
| R4 | Start-up: launch to tray in under 1 s | stopwatch (P13.5 will log it) | Polish, machine | | | |

## Runs

Copy the v1.0.0 tables here, with their results, when the release ships; start the next release's columns empty.
