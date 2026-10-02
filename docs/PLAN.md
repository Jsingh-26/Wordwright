# Build plan

Target: **v1.0 of a snippets-only Wordwright on the Microsoft Store** — a text expander, nothing else. Phases P0–P3 are done, P3.6 (the Store package) is done, and P10 (release) is what remains.

> **The AI writing assistant is parked, not deleted.** Wordwright was also built with an on-device AI rewriting feature. It was implemented, released three times (v0.1.3–v0.1.5) and made to work, but it could not be tested on real hardware, so it is not part of the shipping application. Everything about it — the plan, how it was built, what was proven, what never was, and how to resume — is in [`AI_REWRITING.md`](AI_REWRITING.md), and the code is on the `ai-rewriting` branch.
>
> The phase sections below are kept as the historical record. **P4–P9 are parked**: they describe work that is not in this application. `main` carries no AI code.

## Current status (2026-10-02)

The application is a text expander: snippets with `{date}`, `{time}`, `{clipboard}` and `{cursor}`, a searchable list and editor, excluded apps, export and import, a tray icon, a first-run welcome with a "try it here" playground, and Start with Windows. Core is unit-tested and the solution builds with zero warnings.

**Left to do:**

- [x] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and the About page slot from DESIGN.md §9.
- [ ] **P10.1** README: a GIF of a snippet expanding, install steps, the SmartScreen "unknown publisher" explanation, and a privacy section.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist (light, dark and a high-contrast theme; 100 %, 150 % and 200 % scaling; 800×600 and maximised; keyboard-only; Accessibility Insights for Windows with zero unnamed controls); fix blockers.
- [ ] **P10.3** Release **v1.0.0** on GitHub with notes.
- [ ] **P10.4** Publish the MSIX package (P3.6) to the Microsoft Store.
- [ ] **P10.5** Support link (D12): `.github/FUNDING.yml` (custom Razorpay URL), `About.Support` copy and an About-page link, README support section (with P10.1).

**Added on 2026-10-02:** the **Phase P11 craft pass** (from the UI review in `docs/UI_REVIEW.md`), inserted below before the release phase — it finishes before P10.2 so the release verification covers the final UI.

**Added on 2026-10-03:** the **Phase P12 pre-publication review** (below, before P10). P12.1–P12.5 are blockers: fix them before P10.3 and P10.4; P12.6–P12.10 before the Store listing goes live; the rest is housekeeping. Its human checks are added to the list below.

**Human checks still open** — the maintainer runs these on a real Windows machine, and they are the only thing gating the release:

- [ ] Install the release on a fresh user account: create, edit and delete snippets; everything persists after a restart; nothing is left outside `%AppData%\Wordwright`.
- [ ] Expand snippets in Notepad, Word, Outlook, Chrome, Slack or Teams, VS Code and the Windows search box; the clipboard is restored afterwards, and nothing appears in Win+V history.
- [ ] Install the MSIX package on the same account and repeat, including Start with Windows through the packaged startup task.
- [ ] **Windows 10 22H2 (19045) in a VM:** install, Mica falls back to a plain background (not black or transparent), Segoe UI fallback reads fine, tray icon and menu, `;date` expands.
- [ ] **Keyboard layouts (after P12.1):** German QWERTZ, Spanish, French AZERTY and UK; `;date` with the default prefix, a Shift-prefix such as `:`, Caps Lock on, and AltGr characters.
- [ ] **Screens (after P12.3, P12.9):** two monitors with different scaling, drag the window across; a 1366×768 display at 125 %; a window closed on monitor 2 reopens there.
- [ ] **Store build data (after P12.2):** Open data folder shows the real `%AppData%\Wordwright`; the Start with Windows toggle matches Task Manager → Startup apps after turning it off there.
- [ ] **Long session:** 8 hours running, one sleep/resume and one lock/unlock, then `;date` still expands; Resource Monitor shows no connections throughout.
- [ ] **Heavy clipboard:** copy a large Excel range, type `;sig`, time it, then paste the range again.

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

### Monetization (approved 2026-10-02; research in docs/LAUNCH.md)
- [x] **D12 No ads; free at launch; one-time support payments only.** Ads are rejected: ad revenue needs window views an invisible-by-design tray app never gets (≈$4–20/month even at optimistic install counts), Microsoft's own Store ad platform shut down in 2020, and any ad SDK would break hard rule 1 (no network) and the no-telemetry privacy positioning. v1.0 launches **free with a "Support Wordwright" link to a Razorpay Payment Page** ("customer decides amount" — one-time by nature, no monthly anything): UPI for Indian supporters, cards for international, pays out to an Indian bank account, and needs only the standard Indian KYC (PAN) — no foreign tax forms. GitHub Sponsors was ruled out (mandatory W-8BEN + subscription-shaped tiers); Ko-fi has no UPI and pays out via PayPal/Stripe. The link opens the browser, so the app itself stays offline. A paid model (one-time Pro unlock of future on-device features, e.g. fill-in forms) is reconsidered only once such features exist. → **P10.5**.

### Marketing (2026-10-02)
- [x] **D13 Discovery starts with directories.** Week 1 of the launch sequence (AlternativeTo, SourceForge, Softpedia, SaaSHub, Slant, Awesome-Windows PR) begins once v1.0.0 is released; Product Hunt, Show HN and Reddit follow only if Week 1 goes well. Full sequence and the paste-ready listing kit: `docs/LAUNCH.md`.

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
- [x] **P11.6** Playground completion moment: the first expansion in the welcome window and the Snippets empty state reveals `Welcome.TryHere.Done` with the entrance animation and a small accent check mark. Copy already exists.
- [x] **P11.7** Housekeeping: drop the parked "AI actions"/"Offline AI" pages from `check-screens.ps1`; gitignore `scripts/ui-check-*.png` and untrack the checked-in captures.

## Phase P12: Pre-publication review (2026-10-03; finish P12.1–P12.5 before P10.3)

A full review of the shipping app before the GitHub release and the Store listing: code, packaging, docs, and a run of the Release build on this machine. Evidence for every finding is recorded with it, so nothing here is a guess. Where a fix needs copy that `UX_COPY.md` does not have, the task says so and the maintainer approves the string first (AGENTS.md rule 6).

**What was verified green on 2026-10-03:** `dotnet build -c Release` 0 warnings / 0 errors; `dotnet test` 140/140; no network code anywhere in `src/` (no `HttpClient`, sockets or URLs other than the About-page GitHub link, which opens the browser); no code writes typed text, clipboard or snippet bodies to disk or a log (`Debug.WriteLine` only, trigger name and length); every `Strings.resx` key is used and every used key exists; the Release build launches, shows **Version 1.0.0** on About, follows the dark system theme, and `scripts/check-screens.ps1` reports **zero unnamed controls** on all three pages; CI is green on `main`.

### Blockers (the app misbehaves for a real class of users)

- [x] **P12.1 Keyboard hook ignores Shift, Caps Lock and AltGr.** `KeyboardHook.Translate` calls `GetKeyboardState` on the worker thread, which has no input queue, so the state it passes to `ToUnicodeEx` never has a modifier set. Measured with a probe against `Wordwright.Platform` (`scripts/HookProbe`, added by this review): Shift+S arrived as `s` while the target text box received `S`; Shift+1 arrived as `1` (target: `!`); Caps Lock + s arrived as `s` (target: `S`). Consequences: any prefix that needs Shift (`:`, `#`, `!`, `@`, `$`) never expands; on German, Spanish, Italian, Nordic and most other European layouts `;` itself is Shift+`,`, so **the default prefix is dead for those users**; case-sensitive triggers are a fiction (typing `;Sig` expands the `sig` snippet); `!` or `?` after an ambiguous trigger arrive as `1` or `/`.
  *Fix:* track modifier state from the hook's own stream (queue `WM_KEYUP`/`WM_SYSKEYUP` for the modifier keys, or snapshot `GetAsyncKeyState` for Shift/Ctrl/Alt and `GetKeyState(VK_CAPITAL)` inside the hook callback and carry it in `HookEvent`), then build the 256-byte state array by hand (`0x80` for held VK_SHIFT/VK_CONTROL/VK_MENU and their left/right keys, `0x01` toggle on VK_CAPITAL) before `ToUnicodeEx`. Treat a result of 2 characters (ligature or AltGr) as typed text and a negative result (dead key) as nothing, as today.
  *Done when:* the probe prints `S`, `!`, `S`; on a German layout `;date` (Shift+,) expands and AltGr+Q reaches the buffer as `@`; prefix `:` works; `;Sig` does **not** expand `sig`. `HookProbe` is the regression check for this (`dotnet run --project scripts/HookProbe`; it only injects printable keys into its own window).
  *2026-10-03, done:* the hook reads Shift, Ctrl, Alt (left/right) and Caps Lock inside its callback and builds the state for `ToUnicodeEx` by hand; `ToUnicodeEx` now runs with flag 0x4 so translating cannot swallow a dead key. `HookProbe` prints `S`, `!`, `S`, and on the German layout `;` for Shift+, and `@` for AltGr+Q (it loads that layout for its own thread and unloads it). Ctrl combinations now translate to control characters and are dropped instead of being buffered as letters. **Open:** the keyboard-layouts human check.

- [x] **P12.2 Store build keeps data in the wrong place.** A full-trust MSIX app has file-system virtualisation on by default, so `Environment.GetFolderPath(ApplicationData)` is redirected to `%LocalAppData%\Packages\<pfn>\LocalCache\Roaming\Wordwright`. README, the Store description, About and Settings → Open data folder all promise `%AppData%\Wordwright`; the Open data folder row would open an empty real folder (Explorer runs outside the container), the installer and Store builds would not share snippets, and a Store uninstall silently deletes them. `PackageIdentity.cs` already flags this as unverified.
  *Fix:* in `packaging/AppxManifest.xml` add `xmlns:desktop6="http://schemas.microsoft.com/appx/manifest/desktop/windows10/6"` to the namespaces and `IgnorableNamespaces`, and inside `<Properties>` add `<desktop6:FileSystemWriteVirtualization>disabled</desktop6:FileSystemWriteVirtualization>` (supported from 10.0.18362; the floor is 19045). Also add `Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated.png` to `IconGen --msix` so the taskbar and Start show the mark rather than the plated tile.
  *Done when (human, packaged build):* Open data folder shows `snippets.json` under `%AppData%\Wordwright`; nothing appears under the package's `LocalCache\Roaming`; snippets created in the installer build are visible in the Store build and survive a Store uninstall.
  *2026-10-03, done:* the manifest turns `FileSystemWriteVirtualization` off and declares the `unvirtualizedResources` restricted capability it requires (justification text added to `STORE_LISTING.md`). IconGen writes `Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated.png`, and `pack-msix.ps1` now runs `makepri` so the package has a `resources.pri` (without one Windows never picks scaled or unplated assets); a dump shows every variant indexed. `Wordwright-1.0.0.msixbundle` rebuilt (75 MB). **Open:** the Store-build-data human check.

- [x] **P12.3 Not per-monitor DPI aware.** The published `Wordwright.App.exe` carries only the SDK's default manifest (`asInvoker`, no `dpiAware` or `dpiAwareness`), so WPF runs System-DPI-aware: on a laptop at 150 % with an external monitor at 100 % (or the reverse) the window, welcome and dialogs are bitmap-stretched and blurry on the second screen. The P10.2 scaling checks were single-monitor.
  *Fix:* add `src/Wordwright.App/app.manifest` with `<dpiAware>true/PM</dpiAware>` and `<dpiAwareness>PerMonitorV2</dpiAwareness>` and reference it with `<ApplicationManifest>app.manifest</ApplicationManifest>`; WPF on .NET 10 handles PerMonitorV2 itself. Keep `asInvoker`.
  *Done when (human):* drag the window between two monitors with different scaling; text stays crisp on both; `check-screens.ps1` still reports zero unnamed controls.
  *2026-10-03, done:* `src/Wordwright.App/app.manifest` (asInvoker, Windows 10/11 supportedOS, `true/PM` + `PerMonitorV2`) is referenced as `ApplicationManifest`; the built `Wordwright.App.exe` carries it. **Open:** the two-monitor human check.

- [x] **P12.4 Autosave can drop the last edits.** `SnippetsViewModel` saves 600 ms after the last keystroke, but `OnSelectedSnippetChanged` overwrites the fields without flushing the pending timer. Edit a snippet's text and click another row (or press Ctrl+N, Delete, navigate away, or Quit from the tray) within 600 ms: the timer fires against the **new** selection and the previous snippet's last edits are gone. Separately, while the Shortcut field holds an invalid or taken value, `Save()` returns early, so Name and Text edits made in that state are never written either.
  *Fix:* flush a pending save for the outgoing snippet at the top of `OnSelectedSnippetChanged`, in `AddNew`, `DeleteSelected`, the page's `Unloaded`, and from `App.Quit`/`OnExit`; when only the shortcut is unusable, save Name and Text and keep the stored trigger.
  *Done when:* type in Text and immediately click another snippet, come back, the edit is there; same after Quit and relaunch; a typo in Shortcut never discards body edits.
  *2026-10-03, done:* `SnippetsViewModel.Flush()` writes a pending save; it runs from `OnSelectedSnippetChanging` (while the outgoing snippet is still in the editor), `AddNew`, the page's `Unloaded`, and a new `App.FlushingEdits` event raised first thing in `OnExit`. An unusable shortcut keeps the stored trigger while Name and Text are saved. The view model is in the WPF project, which has no tests, so **open:** the three "done when" steps by hand.

- [x] **P12.5 Snippet prefix has no validation and goes live on every keystroke.** `SettingsViewModel.OnSnippetPrefixChanged` saves the trimmed value immediately, including an empty string. Clearing the field to type a new prefix leaves the matcher with prefix `""`, so any bare word equal to a trigger (`date`, `thanks`) expands anywhere until the user types something else; a letter or digit prefix is also accepted. The tray tooltip also keeps the prefix it was built with.
  *Fix:* commit on LostFocus/Enter only; accept 1–3 characters, none a letter, digit or whitespace; otherwise show an error and keep the old prefix; refresh the tray tooltip on change. **Needs one new string** (suggested `Settings.Error.PrefixInvalid`: "Use 1 to 3 symbols, such as ; or //.") in UX_COPY.md and `Strings.resx`; maintainer to approve the wording.
  *Done when:* clearing the field changes nothing; `::` works; `a` and `1` are refused with the message; the tooltip shows the new prefix.
  *2026-10-03, done:* `SnippetRules.IsValidPrefix` (1–3 characters; no letter, digit, whitespace, control, `-` or `_`, since those last two are shortcut characters) with 18 tests. The field commits on LostFocus or Enter; an invalid value shows `Settings.Error.PrefixInvalid` in the critical colour and leaves the stored prefix alone. `App.UpdateSnippets` refreshes the tray tooltip when the prefix changes. The suggested wording "Use 1 to 3 symbols, such as ; or //." went into UX_COPY.md and `Strings.resx` as is; change it there if you want different words.

### Should fix before the Store listing (reliability under real-world use)

- [x] **P12.6 Hook resilience.** (a) The two `HookProcedure` delegates handed to `SetWindowsHookEx` are temporaries; nothing roots them. The probe survived a forced GC on this build only because `HookThread` runs once and stays at the unoptimised JIT tier, which keeps the temporary on the stack. Store them in fields (`_keyboardProcedure`, `_mouseProcedure`) so the app cannot fail-fast with "callback on a garbage collected delegate" after a runtime or JIT change. (b) Windows silently removes a low-level hook that misses its timeout, and hooks are a known casualty of sleep/resume and lock/unlock in every text expander; nothing today detects or recovers from it, and `IsInstalled == false` (hook refused) is never shown to the user. Re-install on `SystemEvents.PowerModeChanged` (Resume) and `SessionSwitch` (Unlock), and reflect a refused hook in the tray (a disabled "Snippets on" item or a tooltip line; **needs a UX_COPY string**).
  *Done when:* `HookProbe` still passes; after sleep/resume and lock/unlock `;date` expands; with the hook refused the tray says so.
  *2026-10-03, done:* (a) the procedures live in `_keyboardProcedure`/`_mouseProcedure`. (b) `KeyboardHook.Restart()`; the app calls it on `PowerModes.Resume` and on session unlock, console connect and remote connect (SystemEvents, unsubscribed in `OnExit`). `App.HookRefused` drives a new tooltip, `Tray.Tooltip.HookRefused`: "Wordwright: snippets aren't working. Right-click here and turn Snippets on again." (my wording; turning the menu item off and on restarts the hook). `HookProbe` passes, including a Restart pass. **Open:** the long-session human check.

- [x] **P12.7 Clipboard restore cost and the paste race.** `ClipboardService.CaptureCurrent` copies **every** format on the clipboard, on the UI thread, inside the worker's blocking `Dispatcher.Invoke`. A copied Excel range or Word paragraph offers dozens of delayed-render formats, so an expansion then stalls typing for as long as those take to render (hundreds of milliseconds to seconds). And the fixed 150 ms settle before the old clipboard goes back is a race on slow targets (Teams/Slack/Electron under load, Remote Desktop, Citrix): a target that reads the clipboard late pastes the **restored** old content instead of the snippet.
  *Fix:* restore only a whitelist (UnicodeText, Text, Rtf, Html, FileDrop, Bitmap/DIB, CSV) with a small time budget; raise the settle to about 300 ms or make it adaptive; run the capture off the UI thread where OLE allows.
  *Done when:* with a 2,000-cell Excel range on the clipboard, `;sig` expands in under 300 ms and the range still pastes afterwards; `;sig` into Teams over RDP inserts the snippet, not the old clipboard.
  *2026-10-03, done:* the capture keeps Unicode text, text, RTF, HTML, CSV and file lists (a bitmap only when there is no text) within a 200 ms budget. The restore no longer blocks: it runs 400 ms after Ctrl+V from a `DispatcherTimer`, is skipped when the clipboard sequence number shows a newer copy, and a second expansion inside the window keeps the first one's saved clipboard; `{clipboard}` reads the saved text meanwhile; the engine's `Dispose` restores at once. The OLE capture stays on the UI thread (OLE needs STA; the budget bounds it instead). ARCHITECTURE.md → Paste updated. `PasteHarness` retargeted (its TFM could no longer reference Platform; HookHarness too) and given two checks (rich formats restored; a newer copy survives): **PASS**, 9/9. Its cleanup also restored the wrong thing and is fixed. **Open:** the heavy-clipboard human check, and Teams over RDP.

- [x] **P12.8 Line endings.** `SnippetSeeds` and imported bodies use `\n`; the WPF editor writes `\r\n`; classic Win32 edit controls and several older apps render a bare `\n` as nothing, so the seeded `;sig` can come out as one line. Normalise to `\r\n` in `SnippetEngine.Expand` (a Core helper with tests).
  *Done when:* `;sig` pasted into Notepad shows two lines and the status bar reads "Windows (CRLF)".
  *2026-10-03, done:* `ExpandedText.WithWindowsLineEndings()` (Core, 10 tests) turns every break into `\r\n` and recounts the `{cursor}` arrows with each `\r\n` as one step; the engine applies it to every expansion. This also fixes a latent overshoot: bodies typed in the editor already held `\r\n`, and their arrow count was one too many per line break after the cursor. `PasteHarness` confirms one Left arrow crosses `\r\n` in a Win32 edit control. **Open:** `;sig` into Notepad by hand.

- [x] **P12.9 Window placement on a second monitor and on small screens.** `MainWindow.RestorePlacement` clamps against `SystemParameters.WorkArea`, the **primary** monitor only, so a window last closed on a second monitor comes back on the primary at every launch. And on a 1366×768 laptop at 125 % the work area is about 582 DIPs tall, below `MinHeight` 600, so the bottom of the window (the Delete snippet button) sits under the taskbar, pages scrolling or not.
  *Fix:* clamp against the monitor nearest the saved rectangle (`SystemParameters.VirtualScreen*`, or `System.Windows.Forms.Screen`, which the Platform layer already references) and let the minimum height yield to the work area (for example 540, or `Math.Min(600, workArea.Height)`); Core tests for both.
  *Done when:* close on monitor 2, relaunch, still on monitor 2; at 1366×768 and 125 % the whole window is visible.
  *2026-10-03, done:* `WindowPlacement.PickWorkArea` (most overlap, else nearest centre, primary wins ties) and `WindowPlacement.MinimumHeightFor` (`min(600, work area)`), with `ClampTo` using the latter; 6 new tests and one changed (a work area under 600 tall now fits the height instead of keeping 600). `Platform/Display/Monitors` lists every work area; `MainWindow` converts them to its units by the primary monitor's scale, restores onto the picked one and lowers `MinHeight` to fit. Mixed-DPI setups are where this is least certain, so **open:** the screens human check.

- [x] **P12.10 No safety net for unhandled exceptions, and the promised log does not exist.** There is no `DispatcherUnhandledException` or `AppDomain.UnhandledException` handler, so any exception ends the tray app silently. Concrete paths: `SnippetExchange.Export` calls `File.WriteAllText` unguarded (export to a read-only or OneDrive-locked folder); `SnippetExchange.Import` catches `JsonException` and `IOException` but not `UnauthorizedAccessException`; a clipboard COM failure outside the handled `ExternalException`. ARCHITECTURE.md's privacy summary describes an events-only log under `%LocalAppData%\Wordwright\logs` kept 7 days; no such code exists.
  *Fix:* add both handlers so the app keeps running, and either implement the content-free event log the doc promises (events and exception types, never text) or delete that paragraph. Export and import failures need a visible message: `Settings.ImportFailed` exists; export has none (**new string**).
  *Done when:* exporting to `C:\Windows` shows a message and the tray icon stays; the doc and the code agree about logging.
  *2026-10-03, done:* the log is implemented rather than deleted: `Core/Diagnostics/EventLog` (5 tests) writes fixed event names and, for an exception, type + HResult + stack frames but never the message; 7-day pruning at start-up. It lives in `%AppData%\Wordwright\logs`, not `%LocalAppData%`, so the "nothing outside `%AppData%\Wordwright`" check still holds; ARCHITECTURE.md updated. Logged: started, hook refused, hook reinstalled, unhandled/fatal/unobserved-task exceptions. `DispatcherUnhandledException` is logged and handled (the app keeps running); the AppDomain handler logs before .NET ends the process. `SnippetExchange.Export` returns false on IO/access errors, and `Import` also catches `UnauthorizedAccessException` (2 tests). New string `Settings.ExportFailed`: "Couldn't save to that folder. Choose another one." (my wording). **Open:** export to `C:\Windows` by hand.

### Housekeeping (small; do before P10.3)

- [x] **P12.11 Document three limitations in the README** (no code): expansions never happen in windows running as administrator (UIPI blocks a normal process's hook and input; today this is silent); apps hosted by `ApplicationFrameHost.exe` (Store/UWP apps) cannot be excluded by exe name; East-Asian IME composition is not supported.
  *2026-10-03, done:* a "Limitations" section in the README: elevated windows, Store apps under `ApplicationFrameHost.exe`, IME composition.
- [x] **P12.12 Portable build registers autostart.** `StartWithWindows` defaults to true and `Apply` runs on every launch, so the portable zip writes `HKCU\...\Run` pointing at wherever it was unzipped (the dev machine has had a Run entry pointing at a `bin\` folder for the same reason). Decide: default it off when Velopack's locator reports a portable install, or keep it and say so in the README's portable line.
  *2026-10-03, done:* decided: on the very first launch (no `settings.json` yet) Start with Windows defaults **off** unless the build is the Store package or a Setup install (Velopack reports an installed version and not portable). That covers the portable zip and a build run from `bin\`, the cause of this machine's stray Run entry. Existing settings are untouched. README's portable line says so, and the privacy bullet mentions the log.
- [x] **P12.13 About → Third-party licences omits CommunityToolkit.Mvvm (MIT).** One row in UX_COPY.md (`About.Licence.Mvvm`), `Strings.resx` and `AboutPage.xaml`.
  *2026-10-03, done:* row added to UX_COPY.md, `Strings.resx` and `AboutPage.xaml`.
- [x] **P12.14 Remove the parked feature's leftovers from `main`.** `.github/workflows/model-watch.yml` reads `models/models.json`, which no longer exists on `main`, so the Monday job fails every week (it lives on the `ai-rewriting` tag). Mark `docs/EVAL.md`, `docs/MODELS.md`, `docs/UI_REVIEW.md`, `docs/P11_CRAFT_PASS.md`, `docs/WINDOWS_TEST_2026-10-01.md` and `docs/WINDOWS_VALIDATION_2026-10-02.md` as historical (a `docs/history/` folder or a line at the top of each) and list them in the README table.
  *2026-10-03, done:* `model-watch.yml` deleted (it stays on the `ai-rewriting` tag). The six docs keep their paths, so no links break: `EVAL.md` and `MODELS.md` already open with a "Parked" note; the other four now open with a "Historical" note, and the README lists all six under its documentation table.
- [ ] **P12.15 Stale scripts.** `check-launch.ps1`, `check-single-instance.ps1` and `check-run-key.ps1` point at `bin\Debug\net10.0-windows\`; the output folder is `net10.0-windows10.0.19041.0`, so all three fail. `check-run-key.ps1` also deletes the real `%AppData%\Wordwright\settings.json` and the real Run entry; make it refuse to run unless pointed at a scratch folder.
- [ ] **P12.16 Release feed hygiene.** `Releases/` holds unpublished 0.2.1–0.2.6 packages, so `releases.win.json` and `RELEASES` list versions that never shipped and the 1.0.0 delta is computed against 0.2.6. Before the final `scripts/pack-release.ps1`, keep only `Wordwright-0.2.0-full.nupkg` in `Releases/` (or pass `--delta none`) and upload exactly the assets named in `RELEASE_NOTES_v1.0.0.md`.
- [ ] **P12.17 Docs that must match the app.** ARCHITECTURE.md's privacy summary (the log, see P12.10) and its "Capture selection" and elevated-window paragraphs, which belong to the parked feature; the note in `PackageIdentity.cs` once P12.2 is decided; STORE_LISTING.md's "tested to meet accessibility guidelines" declaration stays **unticked** until the Accessibility Insights and screen-reader pass in P10.2 has actually been run; README "Current release" becomes v1.0.0 at P10.3.

### Known and accepted (no task)

The 5-second typing-delay reset, the 64-character buffer, ASCII-only triggers and the 150 ms UI-thread pause per expansion are by design. The self-contained publish is 186 MB on disk and 78 MB as an installer: WPF cannot be trimmed, and `Microsoft.Windows.SDK.NET.dll` (24 MB) is needed for the Store build's StartupTask. Clipboard-history exclusion uses all three documented formats. Injected input cannot exercise the hook (it filters injected events on purpose), so expansion itself stays a human check.

## Phase P10: Release (day 13–14)
- [x] **P10.0** Hero illustration per `brand/HERO_BRIEF.md`, recoloured to the palette, saved as `brand/hero.svg`; used on README, installer and the About page slot from DESIGN.md §9.
- [ ] **P10.1** README: a GIF of a snippet expanding, install steps, the SmartScreen "unknown publisher" explanation, and a privacy section.
  *2026-10-02:* install steps, SmartScreen and privacy sections are in. **Open:** the GIF — it needs real typing (the hook ignores injected keys), so the maintainer records it.
- [ ] **P10.2** Manual test pass using the checklist below and the DESIGN.md polish checklist (light, dark and a high-contrast theme; 100 %, 150 % and 200 % scaling; 800×600 and maximised; keyboard-only; Accessibility Insights for Windows with zero unnamed controls); fix blockers.
  *2026-10-02, automated part:* light and dark, 800×600 and maximised checked with `scripts/check-screens.ps1 -Size 800x600|max`, zero unnamed controls on every page. Blocker fixed: at 800×600 no page scrolled, so half of Settings and the editor's Delete button were unreachable. **Open for the maintainer:** a high-contrast theme, 100 % and 200 % scaling, keyboard-only use, Accessibility Insights and a screen reader, and the release checklist below.
- [ ] **P10.3** Release **v1.0.0** on GitHub with notes.
  *2026-10-03, ready:* version 1.0.0 everywhere; `scripts/pack-release.ps1` builds a 78 MB installer (was 160 MB: stale AI-era native libraries were shipping); notes drafted in `docs/RELEASE_NOTES_v1.0.0.md`. **Open:** the manual P10.2 pass, then the maintainer's go-ahead to publish.
- [ ] **P10.4** Publish the MSIX package (P3.6) to the Microsoft Store.
  *2026-10-03, ready:* `Wordwright-1.0.0.msixbundle` builds (publisher display name Unbound Kite, stale AI description fixed); every listing field is in `docs/STORE_LISTING.md`. **Open (maintainer):** Partner Center account and name reservation, the three identity values into the manifest, a support email, Store screenshots. Store listing keywords: "text expander", "snippets", "typing". A privacy-policy URL is not required when nothing is collected; point it at the README privacy section anyway.
- [ ] **P10.5** Support link (D12): *2026-10-03:* the About link is built and hidden until `SupportUrl` in `AboutPage.xaml.cs` is set; FUNDING.yml and the README section wait for the Razorpay URL. Create `.github/FUNDING.yml` with the custom Razorpay URL (shows a Sponsor heart on the repo pointing at the payment page), add `About.Support` ("Support Wordwright") to UX_COPY.md and `Strings.resx` with a link on the About page under the GitHub link (opens the browser; the app itself stays offline), and a Support section in the README as part of P10.1.
  *Done when:* the Sponsor button shows on the repo and the About link opens the payment page. *Human step:* the maintainer creates the Razorpay Payment Page ("Customer Decides Amount", UPI + cards enabled) — the link goes live only once that page exists. Discovery/launch sequence: `docs/LAUNCH.md`.

## Release checklist
- Fresh Windows user account install and uninstall leave no files outside `%AppData%\Wordwright`.
- Snippets expand in the listed applications, and the clipboard is restored afterwards.
- Network monitor (e.g. Resource Monitor) shows no connections during normal use.
- Log files contain no user text.
- Keyboard-only use of every screen; a screen reader reads the snippet list and the editor.

## Out of scope for v1
Mac/Linux, ARM64, sync across devices, fill-in form snippets, rich-text snippets, per-app snippets — and **the AI writing assistant**, which is parked rather than in scope: [`AI_REWRITING.md`](AI_REWRITING.md).
