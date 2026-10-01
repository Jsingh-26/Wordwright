# Build plan

Target: a public v1.0 on GitHub Releases in about 14 working days, with a snippets-only v0.1 release early so there is always something usable.

## How to run this plan with a coding agent

Repository: https://github.com/Jsingh-26/Wordwright (local folder: `C:\Users\getli\Desktop\Jaspreet Personal Github\Wordwright`).

1. Install the .NET 10 SDK, Git, and the GitHub CLI on Windows. Open a terminal in this repo folder.
2. Start OpenCode on Ollama Cloud: `ollama launch opencode --model glm-5.3:cloud` (if the tag differs, pick GLM-5.3 from the menu).
3. Give the agent one task at a time, using this prompt template:

   > Read AGENTS.md, then do task **P2.3** from docs/PLAN.md. Restate its acceptance criteria first. Build only that task. Run `dotnet build` and `dotnet test`. Tick the checkbox, then commit as `P2.3: <summary>`. Stop and tell me what to check manually, if anything.

4. At each **Human check**, test on your machine before moving on. `scripts/check-screens.ps1` captures every page of the running app and counts controls without an automation name. Paste errors back to the agent exactly as shown.
5. Review each finished phase with a second model (`kimi-k3`): "Review the diff for phase P2 against AGENTS.md hard rules and ARCHITECTURE.md. List violations and bugs only."

## Testing machines and the AI floor (decided 2026-10-01)

The build machine is a 4 GB laptop with an Intel i3-1005G1 and no AI-capable GPU. **The RAM and disk fit rules in `MODELS.md` stay as written**: with Windows running there is around 0.3 GB free, so no catalog model passes "available RAM ≥ model + 1 GB" and Wordwright offers none — the consent dialogue says which kind of "no" it is. This is the documented behaviour, not a bug, and the `minimal` tier's copy ("can run only a very small model") is for machines that do have the memory free for the tiny model. **Do not relax the fit rule to make the 4 GB machine work.**

Everything AI-facing is therefore tested on the maintainer's **16 GB laptop**, where a model can actually load and run:

- **P4 (human check):** the tier and the hardware summary are checked there. The Hardware page is compiled into debug builds only, so an installed release cannot show the raw values — run a debug build on that machine (`dotnet run --project src/Wordwright.App`) to see them.
- **P5.3 (human check):** downloading, verifying and importing a model.
- **P6 and P7 (human checks):** rewriting in real apps, the palette, the pill, and the calibrated times against a stopwatch.

The laptop stays useful as the **minimum-spec machine**: it proves the app installs, expands snippets and refuses AI in the way the docs describe, which the release checklist wants ("works on an 8 GB machine with the `cpu8` recommendation" is the same check one tier down).

**Getting a build onto that machine:** publish a GitHub release (`scripts/pack-release.ps1` then `vpk upload github`), install it there, and report back. A release is worth publishing after any change the check depends on — the assistant should offer.

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
- [ ] **P5.3** Download, verify and failure screens in the dialogue.
- [ ] **P5.4** Import model file (GGUF magic check, hash match against catalog, custom-unverified path).
  *Human check:* download pauses/resumes across a network drop; a tampered file is rejected and deleted; import works.

## Phase P6: On-device rewriting (day 8–9)
- [ ] **P6.1** Inference: `LocalModel` (load with CPU or Vulkan backend per ARCHITECTURE.md, generate with cancellation, apply chat template, thinking-off hint, unload). Idle-unload timer.
- [ ] **P6.2** Core: `PromptBuilder` and `OutputCleaner`. Tests for every cleaner rule, including preambles, quotes, code fences, `<think>` blocks, trailing notes, runaway output.
- [ ] **P6.3** Core: `ActionStore` with the built-in actions from UX_COPY.md; AI actions page (list, editor, reset, Try it).
- [ ] **P6.4** Platform: `HotkeyService` (palette hotkey + per-action hotkeys, re-register on change, in-use/duplicate/reserved validation per ARCHITECTURE.md), selection capture, elevated-window detection. Core tests for hotkey parsing and validation.
- [ ] **P6.5** App: action palette (DESIGN.md §6) near the caret (`GetGUIThreadInfo` caret rect, fallback to mouse), filter, letter shortcuts, custom instruction input.
- [ ] **P6.6** App: progress pill (DESIGN.md §7), Esc cancels, paste result over the selection, all pill messages from UX_COPY. Includes the ruler tick (decision D2): a 1 px Steel track under the pill text with a Forge-ink/Ink-light fill that grows over the estimate for this input length (SpeedEstimator, or the calibrated time once measured); it keeps growing in Ember past the estimate instead of resetting; skipped when Windows animations are off; the pill stays 32 px tall. Copy fallback (decision D5): when the foreground window is elevated at paste time, put the rewrite on the clipboard (plain text, with the exclude-from-history format) and show `Pill.AdminApp.Copied` for 6 s instead of `Pill.AdminApp`; the restore-clipboard step is skipped in that one case, since the copy is the result.
- [ ] **P6.7** App: hotkey recorder control on the AI actions page and Settings; palette shows each action's hotkey; direct hotkeys run the action without the palette.
- [ ] **P6.8** Snippet picker in the palette (decision D6, DESIGN.md §6): below the actions, a `Palette.Snippets` group lists every enabled snippet as "name · {Prefix}shortcut"; the filter box matches name and shortcut; Enter on a snippet expands it at the caret through the normal paste path (variables included) and closes the palette. With no selection and AI off, the palette opens straight to the snippets group instead of the AI-off card. Shows at most 8 rows, then scrolls.
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
- [ ] **P9.1** Complete `eval/cases` to 48 cases per EVAL.md.
- [ ] **P9.2** `eval/cleaner.py` (port of OutputCleaner with the same tests), `run_candidates.py`, `checks.py`, `judge.py`, `spotcheck.py`, `report.py`, `requirements.txt`, `eval/README.md`.
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
