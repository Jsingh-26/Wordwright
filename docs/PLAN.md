# Build plan

Target: **v1.0 of a snippets-only Wordwright on the Microsoft Store** — a text expander, nothing else. Phases P0–P3 are done, P3.6 (the Store package) is done, and P10 (release) is what remains.

> **The AI writing assistant is parked, not deleted.** Wordwright was also built with an on-device AI rewriting feature. It was implemented, released three times (v0.1.3–v0.1.5) and made to work, but it could not be tested on real hardware, so it is not part of the shipping application. Everything about it — the plan, how it was built, what was proven, what never was, and how to resume — is in [`AI_REWRITING.md`](AI_REWRITING.md), and the code is on the `ai-rewriting` branch.
>
> The phase sections below are kept as the historical record. **P4–P9 are parked**: they describe work that is not in this application. `main` carries no AI code.

## Current status (2026-10-02)

The application is a text expander: snippets with `{date}`, `{time}`, `{clipboard}` and `{cursor}`, a searchable list and editor, excluded apps, export and import, a tray icon, a first-run welcome with a "try it here" playground, and Start with Windows. Core is unit-tested and the solution builds with zero warnings.

**Left to do:**

- [ ] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and the About page slot from DESIGN.md §9.
- [ ] **P10.1** README: a GIF of a snippet expanding, install steps, the SmartScreen "unknown publisher" explanation, and a privacy section.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist (light, dark and a high-contrast theme; 100 %, 150 % and 200 % scaling; 800×600 and maximised; keyboard-only; Accessibility Insights for Windows with zero unnamed controls); fix blockers.
- [ ] **P10.3** Release **v1.0.0** on GitHub with notes.
- [ ] **P10.4** Publish the MSIX package (P3.6) to the Microsoft Store.

**Added on 2026-10-02:** the **Phase P11 craft pass** (from the UI review in `docs/UI_REVIEW.md`), inserted below before the release phase — it finishes before P10.2 so the release verification covers the final UI.

**Human checks still open** — the maintainer runs these on a real Windows machine, and they are the only thing gating the release:

- [ ] Install the release on a fresh user account: create, edit and delete snippets; everything persists after a restart; nothing is left outside `%AppData%\Wordwright`.
- [ ] Expand snippets in Notepad, Word, Outlook, Chrome, Slack or Teams, VS Code and the Windows search box; the clipboard is restored afterwards, and nothing appears in Win+V history.
- [ ] Install the MSIX package on the same account and repeat, including Start with Windows through the packaged startup task.

## How to run this plan with a coding agent

1. Install the .NET 10 SDK, Git and the GitHub CLI on Windows. Open a terminal in this repo folder.
2. Give the agent one task at a time, using this prompt template:

   > Read AGENTS.md, then do task **P10.1** from docs/PLAN.md. Restate its acceptance criteria first. Build only that task. Run `dotnet build` and `dotnet test`. Tick the checkbox, then commit as `P10.1: <summary>`. Stop and tell me what to check manually, if anything.

3. At each **Human check**, test on your machine and record the confirmation before moving on.


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

### Craft-pass decisions (from the 2026-10-02 UI review in docs/UI_REVIEW.md)
All five approved by the maintainer on 2026-10-02; details in DESIGN.md and the **Phase P11** tasks below the release phase.
- [x] **D7 Motion for the shipping app.** The time ruler is parked with the AI feature; the signature move becomes the connected animation from the snippet list to the editor, plus one system-wide entrance/exit pattern, everything gated on Windows animations being on. → **P11.4, P11.5, P11.6**.
- [x] **D8 Panel radius.** Apply DESIGN.md's 6 px panel radius (cards, snippet-list selection); controls stay 4 px. → **P11.1**.
- [x] **D9 Error colour.** True validation errors use the system critical fill; Ochre stays caution-only; Ember stays reserved for the parked feature. → **P11.1**.
- [x] **D10 Tray menu.** Style the tray context menu to Fluent (palette, 4 px radius, light/dark following the taskbar). → **P11.3**.
- [x] **D11 Theme setting.** Build the Settings "Theme" row (System / Light / Dark); the copy already exists in UX_COPY.md. → **P11.2**.

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
- [x] **P3.6** Packaging: MSIX package for the Microsoft Store (added at the maintainer's request on 2026-10-01). Full-trust package manifest, visual assets from `brand/`, a Store-ready bundle, and the Start-with-Windows setting using the manifest's `windows.startupTask` extension when the app runs from the package (the `HKCU\...\Run` key stays for the installer build, which Store policy does not allow).
  *Human check:* install from the release on a clean user account; create, edit, delete snippets; everything persists after restart; then the same from the MSIX package, plus Start with Windows working through the packaged startup task.

## Phase P4: Hardware check and recommendation (day 6) — PARKED (see AI_REWRITING.md)
- [x] **P4.1** Platform: `HardwareProbe` returning `HardwareProfile` (ARCHITECTURE.md). A debug page shows the raw values.
- [x] **P4.2** Core: `TierClassifier` per MODELS.md. Tests for every tier boundary.
- [x] **P4.3** Core: `CatalogParser` (schema checks, ignore unknown fields, reject newer major schema) with the embedded `models.json`. Tests with valid, invalid and future-schema files.
- [x] **P4.4** Core: `Recommender` and `SpeedEstimator` per MODELS.md, including the step-down rules. Tests: the worked example must produce "2–4" and "4–9" seconds.
  *Human check:* on your laptop the tier and hardware summary are correct.

## Phase P5: Consent, download, import (day 7) — PARKED (see AI_REWRITING.md)
- [x] **P5.1** "Turn on offline AI" dialogue: recommendation screen with the time ruler (DESIGN.md §5), good at / not so good at, load note, other options, disk and RAM notes.
- [x] **P5.2** Core: `ModelDownloader` (Range resume to `.part`, progress, cancel, free-space check, SHA-256 verify, atomic rename, refuses empty hashes). Tests against a local test HTTP server with a small dummy file.
- [x] **P5.3** Download, verify and failure screens in the dialogue.
- [x] **P5.4** Import model file (GGUF magic check, hash match against catalog, custom-unverified path).
  *Human check:* download pauses/resumes across a network drop; a tampered file is rejected and deleted; import works.

## Phase P6: On-device rewriting (day 8–9) — PARKED (see AI_REWRITING.md)
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

## Phase P7: Calibration and the Offline AI page (day 9–10) — PARKED (see AI_REWRITING.md)
- [ ] **P7.1** Calibration run after download/import; store results; "Done" screen redraws the ruler with measured times.
- [ ] **P7.2** Offline AI page (on and off states), change model, remove model, turn off AI, idle-unload setting.
  *Human check:* measured times look right against a stopwatch on your laptop.

## Phase P8: Better-model check (day 10) — PARKED (see AI_REWRITING.md)
- [ ] **P8.1** Core: `CatalogUpdater` (fetch raw `models.json`, validate, cache, only on click or weekly opt-in, sets `lastCatalogCheckUtc`). Tests with a local test server.
- [ ] **P8.2** Core: better-model rule from MODELS.md. Tests.
- [ ] **P8.3** App: "Check for a better model" button, weekly toggle, banner, tray dot, safe switch (keep old until new is verified and calibrated, "switch back", offer to delete old).
  *Human check:* publish a test catalog on a branch, point a debug build at it, and walk through the whole switch.

## Phase P9: Evaluation and catalog (day 11–12, can run in parallel from P6) — PARKED (see AI_REWRITING.md)
- [x] **P9.1** Complete `eval/cases` to 48 cases per EVAL.md.
- [x] **P9.2** `eval/cleaner.py` (port of OutputCleaner with the same tests), `run_candidates.py`, `checks.py`, `judge.py`, `spotcheck.py`, `report.py`, `requirements.txt`, `eval/README.md`.
- [x] **P9.3** Fill real `source`, `sizeBytes`, `sha256`, `license` for each candidate from Hugging Face; drop any that are gated or not permissively licensed.
- [ ] **P9.4** Run the eval, spot-check, generate `REPORT.md`, set `evalScores`, measured speeds and `status: approved` for the winners.
- [x] **P9.5** `.github/workflows/model-watch.yml`: weekly job that opens an issue for new GGUF releases from the watched publishers. Never edits the catalog.

## Phase P11: Craft pass (2026-10-02; finish before P10.2)

From the UI review in `docs/UI_REVIEW.md`; per-task implementation detail (files, code sketches, acceptance criteria, risks) is in `docs/P11_CRAFT_PASS.md`. Fixes the gaps where the build diverged from DESIGN.md and gives the shipping app the motion spec the parked AI feature took with it. No new dependencies, no new pages, no new copy beyond what UX_COPY.md already holds. **The hero illustration stays P10.0** (it fills the About slot during this pass, but it is generated and recoloured by hand per `brand/HERO_BRIEF.md`).

- [x] **P11.0** Record decisions D7–D11 (below) in DESIGN.md and this plan.
- [x] **P11.1** Theme tokens (D8, D9): add `PanelRadius` (6 px) and apply it to the Settings cards and the snippet-list selection (fields, buttons, menu items stay 4 px); show validation errors in the Fluent critical fill (`SystemFillColorCriticalBrush`, theme-following) via a trigger on `ShortcutMessageIsError` — cautions stay Ochre.
  *Done when:* `check-screens.ps1` shows panels at 6 px; an "already used" error reads as an error and the very-long warning stays Ochre.
- [x] **P11.2** Settings "Theme" row (D11): System / Light / Dark in the Wordwright group (copy exists: `Settings.Theme`), persisted in `settings.json`, applied through the existing accent-swap path in `App.xaml.cs`; the OS watcher only applies in System. Core test for the persisted value.
  *Human check:* each choice re-skins the window immediately and survives a restart.
- [x] **P11.3** Tray menu theming (D10): Fluent-styled context menu (palette brushes, 4 px radius, light/dark following the taskbar via the tray icon's `SystemTheme` logic). No new dependency.
  *Human check:* the menu matches the taskbar theme in both modes.
- [x] **P11.4** `Motion.cs` (D7): one entrance factory (fade + 8 px slide, 200 ms decelerate `cubic-bezier(0,0,0,1)`), one exit (fade, 120 ms accelerate), both strict no-ops when `SystemParameters.ClientAreaAnimation` is false. All later motion goes through it.
- [x] **P11.5** Signature move (D7): connected animation from snippet list to editor (a proxy of the shortcut chip glides from the selected row into the Shortcut field, 200 ms), a choreographed editor settle, page-entrance transitions on navigation, the "Saved" fade-in/out, and list add/remove transitions.
  *Human check:* with "Show animations in Windows" on and off — off must behave exactly as before.
- [ ] **P11.6** Playground completion moment: the first expansion in the welcome window and the Snippets empty state reveals `Welcome.TryHere.Done` with the entrance animation and a small accent check mark. Copy already exists.
- [ ] **P11.7** Housekeeping: drop the parked "AI actions"/"Offline AI" pages from `check-screens.ps1`; gitignore `scripts/ui-check-*.png` and untrack the checked-in captures.

## Phase P10: Release (day 13–14)
- [ ] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and the About page slot from DESIGN.md §9.
- [ ] **P10.1** README: a GIF of a snippet expanding, install steps, the SmartScreen "unknown publisher" explanation, and a privacy section.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist (light, dark and a high-contrast theme; 100 %, 150 % and 200 % scaling; 800×600 and maximised; keyboard-only; Accessibility Insights for Windows with zero unnamed controls); fix blockers.
- [ ] **P10.3** Release **v1.0.0** on GitHub with notes.
- [ ] **P10.4** Publish the MSIX package (P3.6) to the Microsoft Store.

## Release checklist
- Fresh Windows user account install and uninstall leave no files outside `%AppData%\Wordwright`.
- Snippets expand in the listed applications, and the clipboard is restored afterwards.
- Network monitor (e.g. Resource Monitor) shows no connections during normal use.
- Log files contain no user text.
- Keyboard-only use of every screen; a screen reader reads the snippet list and the editor.

## Out of scope for v1
Mac/Linux, ARM64, sync across devices, fill-in form snippets, rich-text snippets, per-app snippets — and **the AI writing assistant**, which is parked rather than in scope: [`AI_REWRITING.md`](AI_REWRITING.md).
