# End-to-end testing

`scripts/E2E` drives the real Wordwright build the way a person would. It types real keys, clicks the window and the tray, pastes into Notepad, Edge, Chrome, VS Code, Word and the Start search box, and reads the results back through UI Automation. It writes a report with screenshots. It replaced most of the "machine" rows of [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

```
dotnet build -c Release
dotnet build scripts/E2E -c Release
scripts\E2E\bin\Release\net10.0-windows10.0.19041.0\E2E.exe
```

Options: `--exe <path>` tests another build, such as the installed one. `--only E1,K1` runs some checks; `--only E1:Word` runs one app of E1. Results go to `scripts/e2e-results/<time>/report.md`, which git ignores.

**Leave the PC alone while it runs** (about 10 minutes). The runner moves the mouse and types. Before every key press it checks that the window in front belongs to something it started. If you click into your own window, it stops rather than typing there.

## Why these checks needed a person, and what changed

| Blocker | Why | Workaround |
|---|---|---|
| Scripted keys never expanded | The hook ignores injected input (`LLKHF_INJECTED`), so it never reacts to its own paste. Every scripted key is injected. | Each key the runner sends carries a private tag in `dwExtraInfo`. Only a Wordwright started in test mode accepts the tag (ARCHITECTURE.md, *End-to-end test mode*). Everything else is the shipping code path. |
| A fresh-user run would use your real snippets and startup entry | The data folder comes from the known-folder API, and `APPDATA` does not redirect it. A launch from a build folder also rewrote the Run key. | In test mode the data folder is a scratch folder and the Run key is never touched. The instance has its own single-instance names, so it runs beside your installed copy. |
| Tray menu "can't be opened by a script" | The icon is in the notification area or its overflow flyout. | UI Automation finds the button by its tooltip, which has the test instance's `;` prefix. The runner right-clicks it and reads the menu items. |
| Keyboard layouts need a person with a German keyboard | Layouts are per thread. Switching the system layout would change your settings. | The runner's own target window loads German, Spanish, French or UK for its thread only. The runner presses the keys a person on that layout would press, `VkKeyScanEx` to key plus Shift or AltGr. |
| Narrator and Accessibility Insights | Both need a person reading or listening. | Axe.Windows (Microsoft, MIT) is the engine behind Accessibility Insights. The runner calls it on every page, and also lists every row's and field's spoken name. |

## What it found (2026-10-03)

| Found by | Problem | Fixed in |
|---|---|---|
| E1 | Notepad typed "v" instead of the snippet. Edge, Word and the Start search box got nothing. Ctrl+V went out with scan code 0 in one batch. | P13.19: real scan codes, 10 ms between the events of Ctrl+V |
| A3 | Narrator read every snippet row as "Wordwright.App.ViewModels.SnippetListItem" | P13.20: rows read the name, then the shortcut |
| A2, A2a | Several controls had no spoken name: the text-box clear button (all text boxes), the welcome window's title-bar buttons, both navigation lists, the snippet list and the excluded-apps list | P13.20 |
| E7, Start search | An app that reads the clipboard late, such as Remote Desktop, a busy PC or the Start search box, pasted your *old* clipboard. The restore ran on a fixed 400 ms timer. | P13.22: the text goes on as a promise, and the clipboard comes back after the paste target has read it, or after 3 s |
| R4 | Launch to first window takes 1.6 to 3.6 s on the 4 GB laptop, over the 1 s target (8 to 13 s when memory is exhausted) | Open: P13.5 |

## Still not automatable here, and why

| Checks | Why | Where it can run |
|---|---|---|
| S1 high contrast, S2 150/200/300 % scaling, S3 two monitors, S4 1366×768, S7 light taskbar, S9 animations off | Each needs a Windows display or accessibility setting changed. The runner does not change system settings on the maintainer's PC. | A VM, or the maintainer toggles the setting and runs `E2E.exe --only S8,S2a,S7`. Window light/dark is covered through the app's own theme setting (S8). |
| W1 Windows 10 | Needs a Windows 10 VM. This laptop has 3.7 GB of RAM and Windows 11 Home, so no Hyper-V and no Windows Sandbox (Pro and above). | The 16 GB laptop with a free VM |
| I1 on a new Windows account | Creating Windows accounts is not something an agent does. | The fresh scratch data folder covers the app's side (I1a, I1b). |
| I4, I5 MSIX install | A self-signed MSIX needs a certificate trusted in the machine store, which is a security setting. | Partner Center validates the Store build. Or the maintainer, once. |
| E7 over real Remote Desktop | Windows 11 Home cannot host RDP. | Covered by the slow-paste target (E7) |
| R1 eight hours with sleep and lock | Unlocking needs the user's password. | The maintainer, overnight |
| Outlook, Teams | They need a signed-in account. | Teams is Chromium, which the Edge and Chrome checks cover. |

## Sources visited (2026-10-03)

| Source | Looked at | Taken | Not taken, because |
|---|---|---|---|
| learn.microsoft.com … ns-winuser-kbdllhookstruct | `flags`, `dwExtraInfo` | `dwExtraInfo` reaches the low-level hook unchanged, which is the basis of the test tag | `LLKHF_LOWER_IL_INJECTED`: not needed |
| github.com/FlaUI/FlaUI | UIA2 vs UIA3 notes | Confirmed UIA3 suits WPF | The package: the built-in `System.Windows.Automation` was enough, so no new dependency |
| github.com/microsoft/axe-windows, nuget Axe.Windows 2.4.2 | `Config.Builder.ForProcessId`, `Scanner.Scan` | The scanner, test-only | The CLI: the NuGet API is simpler in a runner |
| github.com/microsoft/WinAppDriver | Install steps | Nothing | Needs Developer Mode and a service. Last release 2020. |
| github.com/oblitum/Interception | Driver install | Nothing | A kernel driver from a third party, admin install and reboot. The test tag gives the same coverage without it. |
| learn.microsoft.com … windows-sandbox | Editions | Ruled out on this PC | Pro, Enterprise and Education only |
| learn.microsoft.com … clipboard-formats, Clipboard.GetHistoryItemsAsync | History formats and API | Reading Win+V history in E2 | — |
| github.com/VirtualDrivers/Virtual-Display-Driver | Virtual monitors | Nothing | A driver install and a display-setting change |
| learn.microsoft.com … VisualTreeHelper.SetRootDpi | DPI override | Nothing yet | Would test a re-scaled page in-process, not the app at a real scaling |
| espanso.org/docs/configuration/options | `paste_shortcut_event_delay`, `pre_paste_delay` | The gap between the Ctrl+V events (P13.19) | A fixed pre-paste delay: delayed rendering answers it better (P13.22) |
