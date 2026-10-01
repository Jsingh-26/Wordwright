# Build plan

Target: a public v1.0 on GitHub Releases in about 14 working days, with a snippets-only v0.1 release early so there is always something usable.

## Current status and next handoff (reviewed 2026-10-01)

Reviewed baseline: latest release **v0.1.2**; GitHub main at `edf7323`. P0–P6 implementation tasks are ticked; **P7–P10 remain incomplete**. A tick records implementation, not confirmation of a phase's human checks.

The [Windows installation and AI setup report](WINDOWS_TEST_2026-10-01.md) records a successful install and launch on the 16 GB laptop, the no-approved-model dialogue, both placeholder AI pages, and **196 passing Core tests** at the pre-P6 baseline `62d588e`. Since that test, **P6.1–P6.8 (on-device rewriting: engine, prompt/cleaner, actions, hotkeys, palette, pill and snippet picker) have been implemented and pushed**; the Core suite now reports **258 passing tests** and the solution builds with zero warnings. The report is a record of the v0.1.2 release and does **not** confirm live inference, model import/download in the installed app, calibration, the full build at that time, or the P4/P5/P6 human checks. No build containing P6 has been released yet, so the P6 human checks are blocked on a new release.

### Gates before starting P7

- [ ] **P4 human check confirmed by maintainer:** compare the debug Hardware page with this laptop's memory, processor, graphics and disk readings. The release dialogue currently hides those details when there is no approved model, so its no-offer message is not a hardware validation.
- [ ] **P5 human check confirmed by maintainer:** import a real GGUF; confirm its installed-model record and file survive restart. Exercise download interruption/resume and tamper rejection using controlled test data through the existing downloader tests, and record separately which installed-app checks remain blocked by the unapproved catalog. Do not mark the full human check passed from unit tests alone.
- [ ] **P6 human checks confirmed by maintainer:** publish a release containing P6.1–P6.8, install it on the 16 GB laptop, and walk the P6 human checks in this file (palette in Notepad, snippet picker, `Ctrl+Alt+G`, elevated-window copy fallback, in-use hotkey, built-in actions in Word/Outlook/Chrome/Teams, `Ctrl+Z`, Esc, and no user text in the log).
- [ ] Record the maintainer's gate confirmations here or in a linked GitHub issue/PR before moving to P7, as required by AGENTS.md. No confirmation is recorded by the test session. If the catalog still blocks the installed-app download checks, obtain an explicit maintainer decision on staged validation and record the deferred checks for re-testing after P9.4; the test report does not grant that exception.

**2026-10-01 — v0.1.3 published, so the P6 check can run.** [v0.1.3](https://github.com/Jsingh-26/Wordwright/releases/tag/v0.1.3) was packed from main at `7c2975e` and published with the full package, portable zip, installer, a 0.1.2→0.1.3 delta and `RELEASES`. It is the first release containing P6.1–P6.8. Before packing: `dotnet build -c Release` reported 0 warnings and 0 errors, and `dotnet test -c Release` reported 258 passed, 0 failed, 0 skipped. `vpk pack` warns that `VelopackApp.Run()` is called from `App..ctor()` rather than the start of `Main()`; that is pre-existing and should be looked at before update-checking is switched on. **The gate checkboxes above stay unticked until the maintainer records the confirmations** — publishing a build is not itself a confirmation of any human check.

### Next implementation task after the gates: P7.1

Calibration run after download/import: measure a real generation on the 16 GB laptop (or the maintainer's test machine) with a known GGUF, store the result, and have the download/import "Done" screen redraw the time ruler with the measured times. Validate against a stopwatch and record event timings without recording prompts or outputs. Run `dotnet build` and `dotnet test`; then tick and commit P7.1. P7.1 and P7.2 remain unverified until the P6 human checks are confirmed, and calibration measures a model the engine can actually load.

### Verification and release readiness

| Check | Current evidence | Remaining work / owning task |
|---|---|---|
| Install and launch | v0.1.2 installer exit 0; welcome and main window opened | Fresh-user install/uninstall and persistence checks: P3 human check, P10.2 |
| Automated Core checks | 196 passed at baseline `62d588e`; **258 passed, 0 failed, 0 skipped** after the P6 push (`edf7323`) | Full build/test and GitHub CI must pass for every implementation change |
| Hardware fit | Laptop has about 15.87 GB usable RAM; about 0.82 GB available at inspection | Validate with HardwareProbe; free enough RAM for the unchanged model + 1 GB rule: P4 / MODELS.md |
| Model acquisition | No approved models; automatic download disabled; import entry point visible | P5 human check; real source, license, size and SHA-256: P9.3; approval only after P9.4 evaluation |
| Inference and rewrite | P6.1–P6.8 implemented and pushed; **v0.1.3 is the first release containing P6**; builds clean with 258 Core tests | The P6 human checks in the listed Windows apps against v0.1.3; P7.1–P7.2 remains |
| Calibration and activation | Not implemented; importing alone does not turn AI on | P7.1–P7.2; measured times against a stopwatch |
| Privacy and recovery | Core tests are passing; no live rewrite evidence | Offline/network-monitor check, no user text in logs, clipboard restore, Ctrl+Z, Esc cancellation and failure recovery: P6 human checks / P10.2 |
| Safe model replacement | Not implemented | Keep the working model until replacement verification and calibration succeed; switch-back check: P8.3 |

The current catalog is a blocker for the automatic-download path on every laptop, independent of RAM. Manual import is the development route for the P6/P7 checks now that the engine and relevant UI are implemented in source; it does not make the v0.1.2 release capable of rewriting. Do not approve candidates or relax hardware requirements just to unblock a test. P9 retains its existing evaluation requirements and its stated option to run alongside P6; the normal phase gate still applies.

## How to run this plan with a coding agent

Repository: https://github.com/Jsingh-26/Wordwright. Work from a current clone; laptop-specific checkout paths are not prerequisites.

1. Install the .NET 10 SDK, Git, and the GitHub CLI on Windows. Open a terminal in this repo folder.
2. Start OpenCode on Ollama Cloud: `ollama launch opencode --model glm-5.3:cloud` (if the tag differs, pick GLM-5.3 from the menu).
3. Give the agent one task at a time, using this prompt template:

   > Read AGENTS.md, then do task **P2.3** from docs/PLAN.md. Restate its acceptance criteria first. Build only that task. Run `dotnet build` and `dotnet test`. Tick the checkbox, then commit as `P2.3: <summary>`. Stop and tell me what to check manually, if anything.

4. At each **Human check**, test on your machine and record the maintainer confirmation before moving on. `scripts/check-screens.ps1` captures every page and counts unnamed controls where PowerShell is allowed. On a Windows machine that blocks PowerShell, do not invoke `powershell.exe` or `pwsh.exe`: run Git and .NET commands directly, and use an available UI Automation tool for screen checks. Record any check that cannot be performed; an environment limitation is not a pass.
5. Review each finished phase with a second model (`kimi-k3`): "Review the diff for phase P2 against AGENTS.md hard rules and ARCHITECTURE.md. List violations and bugs only."

## Testing machines and the AI floor (decided 2026-10-01)

The build machine is a 4 GB laptop with an Intel i3-1005G1 and no AI-capable GPU. **The RAM and disk fit rules in `MODELS.md` stay as written**: with Windows running there is around 0.3 GB free, so no catalog model passes "available RAM ≥ model + 1 GB" and Wordwright offers none — the consent dialogue says which kind of "no" it is. This is the documented behaviour, not a bug, and the `minimal` tier's copy ("can run only a very small model") is for machines that do have the memory free for the tiny model. **Do not relax the fit rule to make the 4 GB machine work.**

Everything AI-facing is therefore intended to be tested on the maintainer's **16 GB laptop**, after the required implementation exists and enough memory is free for the chosen model:

- **P4 (human check):** the tier and the hardware summary are checked there. The Hardware page is compiled into debug builds only, so an installed release cannot show the raw values — run a debug build on that machine (`dotnet run --project src/Wordwright.App`) to see them.
- **P5.3 (human check):** downloading, verifying and importing a model.
- **P6 and P7 (human checks):** rewriting in real apps, the palette, the pill, and the calibrated times against a stopwatch.

The laptop stays useful as the **minimum-spec machine**: it proves the app installs, expands snippets and refuses AI in the way the docs describe, which the release checklist wants ("works on an 8 GB machine with the `cpu8` recommendation" is the same check one tier down).

**No catalogue model is approved yet** — that happens in P9.4 — so until then the dialogue on that laptop says "no model ready to offer yet" and the catalogue download path cannot be exercised there. **Import model file** is implemented now, but importing only stores and records the file. It does not enable rewriting in v0.1.2. After P6 is implemented, a manually imported GGUF can be used to test rewriting. Calibration and activation remain P7.1.

**Getting a build onto that machine:** publish a GitHub release (`scripts/pack-release.ps1` with an explicit new version, then `vpk upload github`), install it there, and record which commit and checks the build covers. The packaging script defaults to 0.1.0, so do not use its default for a new release. On machines that block PowerShell, use the equivalent direct `dotnet publish` and `vpk pack` commands from that script; do not bypass the policy. A release is worth publishing after any change the check depends on — the assistant should offer.

## Phase P0: Repository and solution (day 1)
- [x] **P0.1** Create `Wordwright.sln` with `src/Wordwright.Core`, `src/Wordwright.Platform`, `src/Wordwright.Inference`, `src/Wordwright.App` (WPF), `tests/Wordwright.Core.Tests` (xUnit), project references as in ARCHITECTURE.md. Add `Directory.Build.props` (nullable enabled, warnings as errors in Core, x64) and `Directory.Packages.props` with pinned versions of the allowed dependencies.
  *Done when:* `dotnet build` and `dotnet test` pass on a clean clone; the app launches and shows an empty window.
- [x] **P0.2** Push to `https://github.com/Jsingh-26/Wordwright` (remote `origin`, branch `main`) and add `.github/workflows/ci.yml`: build + test on `windows-latest` for every push and PR.
  *Done when:* CI is green on GitHub.

## Phase P1: Tray app shell (day 1–2)
- [x] **P1.1** Single-instance app (named mutex; a second launch opens the existing window). Starts to tray, no taskbar button while the window is closed.
- [x] **P1.2** Tray icon from `brand/tray-light-taskbar.svg` and `brand/tray-dark-taskbar.svg` (switch with the taskbar theme), app `.ico` generated from `brand/icon.svg` at 16–256 px and tray menu per DESIGN.md §1, strings from `Strings.resx` built from UX_COPY.md.
- [x] **P1.3** Main window with WPF-UI `FluentWindow`, Mica, NavigationView (Snippets, AI actions, Offline AI, Settings, About) with placeholder pages. Theme follows system. Apply the palette and type from DESIGN.md: Forge ink / Ink light accents, embedded Zodiak for titles and wordmark, Segoe UI Variable for text, Phosphor icons.
- [x] **P1.4** `SettingsStore` in Core (load/save `settings.json`, atomic write, defaults, schema version). Unit tests for defaults, round-trip and corrupt-file fallback (corrupt file is renamed `.corrupt` and defaults are used).
- [x] **P1.5** "Start Wordwright when I sign in" via the `HKCU\...\Run` key.
  *Human check:* tray icon, menu, window, theme switching, start-with-Windows all work.

## Phase P2: Snippet engine (day 3–4)
- [x] **P2.1** Core: `Snippet`, `SnippetStore` (atomic save + `.bak`), validation rules. Tests.
- [x] **P2.2** Core: `TriggerMatcher` (suffix match, word boundary, prefix-conflict rule). Tests: basic match, boundary, `;s` vs `;sig`, case sensitivity, no match inside words like `a;sig`.
- [x] **P2.3** Core: `VariableExpander` (`{date}`, `{time}`, `{clipboard}`, `{cursor}`, `{{`). Tests with an injected clock and clipboard.
- [x] **P2.4** Platform: `KeyboardHook` (WH_KEYBOARD_LL on its own thread, injected-event filter, `ToUnicodeEx`, 64-char buffer, clear rules, excluded apps).
- [x] **P2.5** Platform: `ClipboardService` (save/restore, exclude-from-history format) and `InputSender` (Backspaces, Ctrl+V, Left arrows).
- [x] **P2.6** App: wire hook → matcher → expander → backspaces → paste. Seed three example snippets on first run: `;date`, `;thanks`, `;sig`.
  *Human check:* expansion works in Notepad, Word, Outlook, Chrome (Gmail), Slack or Teams, VS Code, and the Windows search box; clipboard is restored afterwards; nothing appears in Win+V history; a 10,000-character snippet inserts in under 1 s; typing speed does not lag.
- [x] **P2.7** Typing-delay guard (decision D3): `KeystrokeBuffer` clears itself when more than 5 s pass between two keystrokes, using an injected clock so Core stays testable. Tests: a 4.9 s gap still expands, a 5.1 s gap does not, and the character typed after the gap starts a fresh buffer. No settings entry in v1; no copy needed.
  *Human check:* type `;si`, wait six seconds, type `g` → nothing expands; type `;sig` normally → expands.

## Decisions needed (from the 2026-10-01 design study in DESIGN.md)
Two product changes the study recommended. Both approved by the maintainer on 2026-10-01; the copy is in UX_COPY.md.
- [x] **D1 "Try it here" playground.** The welcome window (and the empty Snippets list) gets a one-line text box where typing `;date` expands in place, instead of sending the user to another app to try it. Mechanism borrowed from Espanso's welcome window; none of the Windows expanders, Text Blaze included, do it. → **P3.4c**.
- [x] **D3 Typing-delay guard** (from Text Blaze). The keystroke buffer clears after 5 s with no keystroke, so a shortcut typed with a long pause in it never expands. → **P2.7**.
- [x] **D4 Live preview under the Text box** (from Text Blaze's Preview). → folded into **P3.4c**.
- [x] **D5 Copy as the fallback** (from AI Blaze). → folded into **P6.6**.
- [x] **D6 Snippet picker in the palette** (every expander has one). Pulled into v1 → **P6.8**.
- [x] **D2 Pill ruler tick.** The progress pill (DESIGN.md §7) shows a hairline ruler that fills against the measured estimate, so every rewrite echoes the time ruler. Must stay inside the 32 px pill and skip when Windows animations are off. → folded into **P6.6**.

## Phase P3: Snippet manager UI (day 4–5)
- [x] **P3.1** Snippets page: searchable list + editor (DESIGN.md §2), auto-save, "Saved" indicator, validation messages, delete with confirmation.
- [x] **P3.2** Insert buttons (Date, Time, Clipboard, Cursor position) insert variables at the caret.
- [x] **P3.3** Settings page: snippet prefix, excluded apps, export/import snippets (JSON), open data folder.
- [x] **P3.4** First-run welcome (UX_COPY "First run").
- [x] **P3.4a** Window basics found missing in the 2026-10-01 design study: `MinWidth` 800 / `MinHeight` 600 on the main window; remember size, position and maximised state in `settings.json` (`window` object, clamped to the current work area on restore); keyboard accelerators Ctrl+N (new snippet), Ctrl+F (focus search), Delete on a selected list row (opens the same confirmation), Esc closes dialogs. Core tests for the clamp rule.
- [x] **P3.4b** Settings and About per DESIGN.md §8 and §9: grouped `CardControl` rows with Remove inside each excluded-app row; About shows the version (from the assembly), the GitHub link in the brand accent instead of the system blue, and a "Third-party licences" expander listing Zodiak, Phosphor, WPF-UI, H.NotifyIcon, LLamaSharp and Velopack with their licence names. Later phases add their rows into the groups rather than appending to the page. Also give automation names to the four controls `scripts/check-screens.ps1` found unnamed on 2026-10-01: WPF-UI's `TitleBarMinimizeButton`, `TitleBarMaximizeButton`, `TitleBarCloseButton` and `NavigationToggleButton` (set `AutomationProperties.Name` through the TitleBar/NavigationView templates, or on the elements after load; strings go in UX_COPY under a new "Accessibility" table).
- [x] **P3.4c** "Try it here" playground (decision D1): a single-line text box on the welcome window under the body text, and in the Snippets page empty state, labelled with `Welcome.TryHere` / `Snippets.Empty.TryHere`. The snippet engine expands inside it exactly as in any other app (same hook, no special path), so the box is a plain `ui:TextBox` with the Wordwright window not in `excludedApps`. After the first expansion the label changes to `Welcome.TryHere.Done`. The box never persists its text. Welcome window stays `SizeToContent`. Also the live preview (decision D4): a `SecondaryText` line under the Text box, `Snippets.Preview` followed by the body with `{date}` and `{time}` resolved through `VariableExpander` and `{cursor}` shown as `|`; updates on every keystroke; collapsed when the body contains no `{…}` variable; `{clipboard}` is shown as `Snippets.Preview.Clipboard` rather than the real clipboard, so the preview never reads it.
  *Human check:* resize below 800×600 is refused; close, reopen and the window is where it was; Ctrl+N, Ctrl+F, Delete work; `scripts/check-screens.ps1` reports zero unnamed controls on Settings and About; typing `;date` in the welcome box expands in place and the label switches to the done text.
- [x] **P3.5** Packaging: Velopack installer published to GitHub Releases as **v0.1.0 (snippets only)**. App-update checking stays off by default.
- [ ] **P3.6** Packaging: MSIX package for the Microsoft Store (added at the maintainer's request on 2026-10-01). Full-trust package manifest, visual assets from `brand/`, a Store-ready bundle, and the Start-with-Windows setting using the manifest's `windows.startupTask` extension when the app runs from the package (the `HKCU\...\Run` key stays for the installer build, which Store policy does not allow).
  *Human check:* install from the release on a clean user account; create, edit, delete snippets; everything persists after restart; then the same from the MSIX package, plus Start with Windows working through the packaged startup task.

## Phase P4: Hardware check and recommendation (day 6)
- [x] **P4.1** Platform: `HardwareProbe` returning `HardwareProfile` (ARCHITECTURE.md). A debug page shows the raw values.
- [x] **P4.2** Core: `TierClassifier` per MODELS.md. Tests for every tier boundary.
- [x] **P4.3** Core: `CatalogParser` (schema checks, ignore unknown fields, reject newer major schema) with the embedded `models.json`. Tests with valid, invalid and future-schema files.
- [x] **P4.4** Core: `Recommender` and `SpeedEstimator` per MODELS.md, including the step-down rules. Tests: the worked example must produce "2–4" and "4–9" seconds.
  *Human check:* on your laptop the tier and hardware summary are correct.

## Phase P5: Consent, download, import (day 7)
- [x] **P5.1** "Turn on offline AI" dialogue: recommendation screen with the time ruler (DESIGN.md §5), good at / not so good at, load note, other options, disk and RAM notes.
- [x] **P5.2** Core: `ModelDownloader` (Range resume to `.part`, progress, cancel, free-space check, SHA-256 verify, atomic rename, refuses empty hashes). Tests against a local test HTTP server with a small dummy file.
- [x] **P5.3** Download, verify and failure screens in the dialogue.
- [x] **P5.4** Import model file (GGUF magic check, hash match against catalog, custom-unverified path).
  *Human check:* download pauses/resumes across a network drop; a tampered file is rejected and deleted; import works.

## Phase P6: On-device rewriting (day 8–9)
- [x] **P6.1** Inference: `LocalModel` (load with CPU or Vulkan backend per ARCHITECTURE.md, generate with cancellation, apply chat template, thinking-off hint, unload). Idle-unload timer.
- [x] **P6.2** Core: `PromptBuilder` and `OutputCleaner`. Tests for every cleaner rule, including preambles, quotes, code fences, `<think>` blocks, trailing notes, runaway output.
- [x] **P6.3** Core: `ActionStore` with the built-in actions from UX_COPY.md; AI actions page (list, editor, reset, Try it).
- [x] **P6.4** Platform: `HotkeyService` (palette hotkey + per-action hotkeys, re-register on change, in-use/duplicate/reserved validation per ARCHITECTURE.md), selection capture, elevated-window detection. Core tests for hotkey parsing and validation.
- [x] **P6.5** App: action palette (DESIGN.md §6) near the caret (`GetGUIThreadInfo` caret rect, fallback to mouse), filter, letter shortcuts, custom instruction input.
- [x] **P6.6** App: progress pill (DESIGN.md §7), Esc cancels, paste result over the selection, all pill messages from UX_COPY. Includes the ruler tick (decision D2): a 1 px Steel track under the pill text with a Forge-ink/Ink-light fill that grows over the estimate for this input length (SpeedEstimator, or the calibrated time once measured); it keeps growing in Ember past the estimate instead of resetting; skipped when Windows animations are off; the pill stays 32 px tall. Copy fallback (decision D5): when the foreground window is elevated at paste time, put the rewrite on the clipboard (plain text, with the exclude-from-history format) and show `Pill.AdminApp.Copied` for 6 s instead of `Pill.AdminApp`; the restore-clipboard step is skipped in that one case, since the copy is the result.
- [x] **P6.7** App: hotkey recorder control on the AI actions page and Settings; palette shows each action's hotkey; direct hotkeys run the action without the palette.
- [x] **P6.8** Snippet picker in the palette (decision D6, DESIGN.md §6): below the actions, a `Palette.Snippets` group lists every enabled snippet as "name · {Prefix}shortcut"; the filter box matches name and shortcut; Enter on a snippet expands it at the caret through the normal paste path (variables included) and closes the palette. With no selection and AI off, the palette opens straight to the snippets group instead of the AI-off card. Shows at most 8 rows, then scrolls.
  *Human check:* palette hotkey in Notepad with nothing selected → snippets listed; typing "sig" filters; Enter inserts the signature; Esc leaves the text untouched.
  *Human check:* Ctrl+Alt+G fixes grammar directly and Ctrl+Alt+Space opens the palette; a rewrite aimed at an elevated Notepad ends with the "Copied" pill and the text on the clipboard; a hotkey already used by another app shows the in-use message; every built-in action works in Notepad, Word, Outlook, Chrome and Teams; Ctrl+Z restores the original; Esc cancels cleanly; no text is written to the log file.

## Phase P7: Calibration and the Offline AI page (day 9–10)
- [ ] **P7.1** Calibration run after download/import; store results; "Done" screen redraws the ruler with measured times.
- [ ] **P7.2** Offline AI page (on and off states), change model, remove model, turn off AI, idle-unload setting.
  *Human check:* measured times look right against a stopwatch on your laptop.

## Phase P8: Better-model check (day 10)
- [ ] **P8.1** Core: `CatalogUpdater` (fetch raw `models.json`, validate, cache, only on click or weekly opt-in, sets `lastCatalogCheckUtc`). Tests with a local test server.
- [ ] **P8.2** Core: better-model rule from MODELS.md. Tests.
- [ ] **P8.3** App: "Check for a better model" button, weekly toggle, banner, tray dot, safe switch (keep old until new is verified and calibrated, "switch back", offer to delete old).
  *Human check:* publish a test catalog on a branch, point a debug build at it, and walk through the whole switch.

## Phase P9: Evaluation and catalog (day 11–12, can run in parallel from P6)
- [x] **P9.1** Complete `eval/cases` to 48 cases per EVAL.md.
- [x] **P9.2** `eval/cleaner.py` (port of OutputCleaner with the same tests), `run_candidates.py`, `checks.py`, `judge.py`, `spotcheck.py`, `report.py`, `requirements.txt`, `eval/README.md`.
- [ ] **P9.3** Fill real `source`, `sizeBytes`, `sha256`, `license` for each candidate from Hugging Face; drop any that are gated or not permissively licensed.
- [ ] **P9.4** Run the eval, spot-check, generate `REPORT.md`, set `evalScores`, measured speeds and `status: approved` for the winners.
- [ ] **P9.5** `.github/workflows/model-watch.yml`: weekly job that opens an issue for new GGUF releases from the watched publishers. Never edits the catalog.

## Phase P10: Release (day 13–14)
- [ ] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and the About page slot from DESIGN.md §9.
- [ ] **P10.1** README: GIF of a snippet expansion and a rewrite, install steps, the SmartScreen "unknown publisher" explanation, privacy section, link to REPORT.md.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist (light, dark and a high-contrast theme; 100 %, 150 % and 200 % scaling; 800×600 and maximised; keyboard-only; Accessibility Insights for Windows with zero unnamed controls); fix blockers.
- [ ] **P10.3** Release **v1.0.0** on GitHub with notes.
- [ ] **P10.4** 60–90 s demo video: snippet → turn on offline AI (time ruler) → rewrite in Outlook → Ctrl+Z.

## Release checklist
- Fresh Windows user account install and uninstall leave no files outside `%AppData%\Wordwright` and `%LocalAppData%\Wordwright`.
- With Wi-Fi off: snippets and AI rewriting work; the only errors are on "Check for a better model".
- Network monitor (e.g. Resource Monitor) shows no connections during normal use.
- Log files contain no user text.
- Works on an 8 GB machine with the `cpu8` recommendation.
- Keyboard-only use of every screen; screen reader reads the palette items.

## Out of scope for v1
Mac/Linux, ARM64, cloud providers, sync across devices, fill-in form snippets, rich-text snippets, per-app snippets, streaming the rewrite into the app, translation as a built-in action.
