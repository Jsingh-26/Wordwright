# Windows installation and AI setup test — 2026-10-01

## Build and scope

Tested the latest published Windows installer, [v0.1.2](https://github.com/Jsingh-26/Wordwright/releases/tag/v0.1.2), and reviewed a fresh clone of GitHub main at `62d588e841091acf4fa1d272771c5083fe1aa928`. Existing laptop source was not used or modified. Tests used the desktop execution environment on Windows 11 x64; this was not a clean Windows user account or a Microsoft Store package test.

Hardware observed through the host runtime: AMD Ryzen 5 7535HS, 12 logical processors, approximately 15.87 GB usable total RAM and 0.82 GB available RAM at inspection. These observations do not establish the app's HardwareProbe tier, AVX2 reading or dedicated GPU memory. The debug Hardware page still needs the P4 human check. A 16 GB label alone does not guarantee a model fits; retain the available-RAM and disk requirements in MODELS.md.

## Observed results

| Action | Result |
|---|---|
| Look for an existing app | No app found in the usual installation locations, running-app inventory or uninstall registry. A downloaded `wordwright.zip` contained source, not an executable. |
| Download the release installer | Successful; roughly nine minutes on this connection. |
| Install v0.1.2 for the current user | Successful; Velopack setup exited 0 and reported Desktop and Start Menu shortcut creation. |
| Launch installed executable | Welcome window opened. Relaunch subsequently opened the main window. |
| Inspect Snippets page | Three seed snippets visible: `;date`, `;thanks`, `;sig`. Expansion into external apps was not tested. |
| Click Turn on offline AI | Dialogue displayed: “There's no model ready to offer yet. Wordwright will check again later.” |
| Inspect available AI setup actions | Download and turn on disabled; Import model file instead available; Not now closed the dialogue. |
| Open AI actions and Offline AI | Both pages displayed only placeholder headings. |
| Run Core tests | `dotnet test tests/Wordwright.Core.Tests -c Release`: 196 passed, 0 failed, 0 skipped. Includes automated acquisition and hardware-recommendation checks; no live inference coverage. |
| Attempt full source build | `dotnet build -c Release` encountered repeated incomplete NuGet downloads for LLamaSharp.Backend.Cpu and WPF-UI. Build was stopped; full build validation is incomplete. |

The installed process was observed under the desktop host's package-local cache. Standard profile installation/uninstallation, shortcut launch outside the host and data-location behavior were not independently verified in this session.

## Why rewriting cannot be tested yet

- The embedded catalog has five candidate entries and no approved models. Download sources, sizes and SHA-256 values are unfinished. The no-offer dialogue is expected with this catalog; it does not prove a hardware incompatibility.
- `Wordwright.Inference` contains its project and dependencies but no `LocalModel` implementation.
- P6.1–P6.8 and P7.1–P7.2 are unchecked. Hotkey rewriting, action UI and calibration/activation are not implemented.
- Import stores and registers a model; it does not turn AI on or make v0.1.2 rewrite text.
- The dialogue's “check again later” wording is not evidence of an automatic check: catalog updating remains P8.

No model was downloaded or imported. No inference, rewrite output, GPU backend or rewrite-speed result is claimed.

## Checks still outstanding

- P4: validate the app's hardware readings and tier on the debug page.
- P5: real-model import and persistence, installed-app download interruption/resume, tamper rejection and consent flow. The catalog blocks the ordinary download path; passing the Core tests is separate evidence.
- P6/P7: real GGUF loading/generation once implemented, all built-in actions in the listed Windows applications, hotkeys, clipboard restoration, cancellation, undo, unload behavior and calibration against a stopwatch.
- P10: clean-user installation/uninstallation, theme/scaling/accessibility, offline operation, network-monitor verification and no user text in logs.

Follow the gate checklist and next-task handoff in [PLAN.md](PLAN.md). This report does not confirm the maintainer's phase gates, approve a model, or complete any P6/P7 task.
