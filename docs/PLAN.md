# Build plan

Target: a public v1.0 on GitHub Releases in about 14 working days, with a snippets-only v0.1 release early so there is always something usable.

## How to run this plan with a coding agent

Repository: https://github.com/Jsingh-26/Wordwright (local folder: `C:\Users\getli\Desktop\Jaspreet Personal Github\Wordwright`).

1. Install the .NET 10 SDK, Git, and the GitHub CLI on Windows. Open a terminal in this repo folder.
2. Start OpenCode on Ollama Cloud: `ollama launch opencode --model glm-5.3:cloud` (if the tag differs, pick GLM-5.3 from the menu).
3. Give the agent one task at a time, using this prompt template:

   > Read AGENTS.md, then do task **P2.3** from docs/PLAN.md. Restate its acceptance criteria first. Build only that task. Run `dotnet build` and `dotnet test`. Tick the checkbox, then commit as `P2.3: <summary>`. Stop and tell me what to check manually, if anything.

4. At each **Human check**, test on your machine before moving on. Paste errors back to the agent exactly as shown.
5. Review each finished phase with a second model (`kimi-k3`): "Review the diff for phase P2 against AGENTS.md hard rules and ARCHITECTURE.md. List violations and bugs only."

## Phase P0: Repository and solution (day 1)
- [x] **P0.1** Create `Wordwright.sln` with `src/Wordwright.Core`, `src/Wordwright.Platform`, `src/Wordwright.Inference`, `src/Wordwright.App` (WPF), `tests/Wordwright.Core.Tests` (xUnit), project references as in ARCHITECTURE.md. Add `Directory.Build.props` (nullable enabled, warnings as errors in Core, x64) and `Directory.Packages.props` with pinned versions of the allowed dependencies.
  *Done when:* `dotnet build` and `dotnet test` pass on a clean clone; the app launches and shows an empty window.
- [ ] **P0.2** Push to `https://github.com/Jsingh-26/Wordwright` (remote `origin`, branch `main`) and add `.github/workflows/ci.yml`: build + test on `windows-latest` for every push and PR.
  *Done when:* CI is green on GitHub.

## Phase P1: Tray app shell (day 1–2)
- [ ] **P1.1** Single-instance app (named mutex; a second launch opens the existing window). Starts to tray, no taskbar button while the window is closed.
- [ ] **P1.2** Tray icon from `brand/tray-light-taskbar.svg` and `brand/tray-dark-taskbar.svg` (switch with the taskbar theme), app `.ico` generated from `brand/icon.svg` at 16–256 px and tray menu per DESIGN.md §1, strings from `Strings.resx` built from UX_COPY.md.
- [ ] **P1.3** Main window with WPF-UI `FluentWindow`, Mica, NavigationView (Snippets, AI actions, Offline AI, Settings, About) with placeholder pages. Theme follows system. Apply the palette and type from DESIGN.md: Forge ink / Ink light accents, embedded Zodiak for titles and wordmark, Segoe UI Variable for text, Phosphor icons.
- [ ] **P1.4** `SettingsStore` in Core (load/save `settings.json`, atomic write, defaults, schema version). Unit tests for defaults, round-trip and corrupt-file fallback (corrupt file is renamed `.corrupt` and defaults are used).
- [ ] **P1.5** "Start Wordwright when I sign in" via the `HKCU\...\Run` key.
  *Human check:* tray icon, menu, window, theme switching, start-with-Windows all work.

## Phase P2: Snippet engine (day 3–4)
- [ ] **P2.1** Core: `Snippet`, `SnippetStore` (atomic save + `.bak`), validation rules. Tests.
- [ ] **P2.2** Core: `TriggerMatcher` (suffix match, word boundary, prefix-conflict rule). Tests: basic match, boundary, `;s` vs `;sig`, case sensitivity, no match inside words like `a;sig`.
- [ ] **P2.3** Core: `VariableExpander` (`{date}`, `{time}`, `{clipboard}`, `{cursor}`, `{{`). Tests with an injected clock and clipboard.
- [ ] **P2.4** Platform: `KeyboardHook` (WH_KEYBOARD_LL on its own thread, injected-event filter, `ToUnicodeEx`, 64-char buffer, clear rules, excluded apps).
- [ ] **P2.5** Platform: `ClipboardService` (save/restore, exclude-from-history format) and `InputSender` (Backspaces, Ctrl+V, Left arrows).
- [ ] **P2.6** App: wire hook → matcher → expander → backspaces → paste. Seed three example snippets on first run: `;date`, `;thanks`, `;sig`.
  *Human check:* expansion works in Notepad, Word, Outlook, Chrome (Gmail), Slack or Teams, VS Code, and the Windows search box; clipboard is restored afterwards; nothing appears in Win+V history; a 10,000-character snippet inserts in under 1 s; typing speed does not lag.

## Phase P3: Snippet manager UI (day 4–5)
- [ ] **P3.1** Snippets page: searchable list + editor (DESIGN.md §2), auto-save, "Saved" indicator, validation messages, delete with confirmation.
- [ ] **P3.2** Insert buttons (Date, Time, Clipboard, Cursor position) insert variables at the caret.
- [ ] **P3.3** Settings page: snippet prefix, excluded apps, export/import snippets (JSON), open data folder.
- [ ] **P3.4** First-run welcome (UX_COPY "First run").
- [ ] **P3.5** Packaging: Velopack installer published to GitHub Releases as **v0.1.0 (snippets only)**. App-update checking stays off by default.
  *Human check:* install from the release on a clean user account; create, edit, delete snippets; everything persists after restart.

## Phase P4: Hardware check and recommendation (day 6)
- [ ] **P4.1** Platform: `HardwareProbe` returning `HardwareProfile` (ARCHITECTURE.md). A debug page shows the raw values.
- [ ] **P4.2** Core: `TierClassifier` per MODELS.md. Tests for every tier boundary.
- [ ] **P4.3** Core: `CatalogParser` (schema checks, ignore unknown fields, reject newer major schema) with the embedded `models.json`. Tests with valid, invalid and future-schema files.
- [ ] **P4.4** Core: `Recommender` and `SpeedEstimator` per MODELS.md, including the step-down rules. Tests: the worked example must produce "2–4" and "4–9" seconds.
  *Human check:* on your laptop the tier and hardware summary are correct.

## Phase P5: Consent, download, import (day 7)
- [ ] **P5.1** "Turn on offline AI" dialogue: recommendation screen with the time ruler (DESIGN.md §5), good at / not so good at, load note, other options, disk and RAM notes.
- [ ] **P5.2** Core: `ModelDownloader` (Range resume to `.part`, progress, cancel, free-space check, SHA-256 verify, atomic rename, refuses empty hashes). Tests against a local test HTTP server with a small dummy file.
- [ ] **P5.3** Download, verify and failure screens in the dialogue.
- [ ] **P5.4** Import model file (GGUF magic check, hash match against catalog, custom-unverified path).
  *Human check:* download pauses/resumes across a network drop; a tampered file is rejected and deleted; import works.

## Phase P6: On-device rewriting (day 8–9)
- [ ] **P6.1** Inference: `LocalModel` (load with CPU or Vulkan backend per ARCHITECTURE.md, generate with cancellation, apply chat template, thinking-off hint, unload). Idle-unload timer.
- [ ] **P6.2** Core: `PromptBuilder` and `OutputCleaner`. Tests for every cleaner rule, including preambles, quotes, code fences, `<think>` blocks, trailing notes, runaway output.
- [ ] **P6.3** Core: `ActionStore` with the built-in actions from UX_COPY.md; AI actions page (list, editor, reset, Try it).
- [ ] **P6.4** Platform: `HotkeyService` (palette hotkey + per-action hotkeys, re-register on change, in-use/duplicate/reserved validation per ARCHITECTURE.md), selection capture, elevated-window detection. Core tests for hotkey parsing and validation.
- [ ] **P6.5** App: action palette (DESIGN.md §6) near the caret (`GetGUIThreadInfo` caret rect, fallback to mouse), filter, letter shortcuts, custom instruction input.
- [ ] **P6.6** App: progress pill (DESIGN.md §7), Esc cancels, paste result over the selection, all pill messages from UX_COPY.
- [ ] **P6.7** App: hotkey recorder control on the AI actions page and Settings; palette shows each action's hotkey; direct hotkeys run the action without the palette.
  *Human check:* Ctrl+Alt+G fixes grammar directly and Ctrl+Alt+Space opens the palette; a hotkey already used by another app shows the in-use message; every built-in action works in Notepad, Word, Outlook, Chrome and Teams; Ctrl+Z restores the original; Esc cancels cleanly; no text is written to the log file.

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
- [ ] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and About page.
- [ ] **P10.1** README: GIF of a snippet expansion and a rewrite, install steps, the SmartScreen "unknown publisher" explanation, privacy section, link to REPORT.md.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist; fix blockers.
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
