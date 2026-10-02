# UI review and craft pass — 2026-10-02

> **Historical.** The UI review of 2026-10-02 that led to Phase P11; its findings are done. The current design is in [DESIGN.md](DESIGN.md).

A craft-studio review of the shipping (snippets-only) UI, run against `docs/DESIGN.md`, `docs/UX_COPY.md` and the desktop-app playbook. Method: read the code, build and run the app, capture every page with `scripts/check-screens.ps1`, compare against the spec. Screenshots: `scripts/ui-check-Snippets.png`, `ui-check-Settings.png`, `ui-check-About.png` (fresh, current build, dark theme).

## Study note (refresh of the 2026-10-01 one, for the shipping app)

- **Medium / platform:** Windows 11 desktop app, WPF + WPF-UI 4.3 (Fluent, Mica), tray-resident. Playbook: desktop-app.
- **What it is:** a text expander, and nothing else. The AI rewriting feature — and with it the time-ruler signature screen — is parked on the `ai-rewriting` branch.
- **Who, where, how long:** non-technical people who write all day. The window is opened rarely; the daily surfaces are the tray icon, the tray menu, and the expansions themselves inside other apps.
- **Keep (verified in the running build, dark theme):** Forge ink / Ink light accent with the tinted Steel/Anvil neutrals, Zodiak titles and wordmark, Phosphor nav icons, Mica, the two-line wordmark, the split list/editor, the "Try it here" playground, live "Inserts as:" preview, saved indicator, grouped Settings, automation names (zero unnamed controls on all three pages).
- **Niche references (from the 2026-10-01 study):** Text Blaze, Beeftext, TextExpander, PhraseExpress, Espanso. Shared conventions are all honoured. What nobody else does: the in-app playground, privacy as the headline, no accounts or caps.
- **What changed since the first study:** **the signature moment shipped away with the AI feature.** The time ruler was the one bold visual, and the shipping app has no replacement — and no animation of any kind (zero storyboards in the codebase). That, more than any spacing or colour, is why the UI reads as "basic".
- **Candidate signature moves (playbook menu):** 2 command palette (parked with AI) · 3 connected animation list→editor · 6 progressive onboarding playground (already built) · 7 ambient tray status. **Chosen: 3, plus strengthening 6.** The playground is the product's own idea and already exists — amplify it with a completion moment. The connected animation is Fluent's signature transition, answers "make it interactive" without inventing product behaviour, and no expander in the niche does it. 7 rejected: a status that animates on every expansion is exactly the playbook's repeating-animation trap; the tray stays quiet by design.
- **Open questions for the maintainer:** the decision list at the end (D7–D10).

## What's already good

The bones are right and the spec compliance is high. The palette exists as named resources with the "no other colours may be used" comment; Zodiak is embedded and used exactly where DESIGN.md says; the wordmark uses the real pen-nib mark geometry; all strings come from `Strings.resx`; window min size, remembered placement and accelerators (Ctrl+N/Ctrl+F/Delete) work; accessibility is in better shape than most shipped apps.

"Basic" here does not mean wrong. It means: **static** (no motion anywhere), **unfinished in three visible places** (About hero, tray menu, radius hierarchy), and **never checked in the light theme**.

## Findings

### F1 — No motion at all (the main reason it feels basic)
Zero `Storyboard`/`VisualStateManager`/animation code in `src/`. Page changes snap, "Saved" toggles visibility, list items appear and vanish instantly, selection jumps. The platform expectation (and the playbook's audience section) is Fluent's short, directional, eased motion. The design's motion idea — "a stroke being drawn, left to right, ease-out, once" — only existed on the parked AI screens, so the shipping app never got a motion spec.

### F2 — The signature moment is parked
The time ruler was spend-boldness-once. With it gone, everything in the window is disciplined-and-quiet with nothing bold to balance. The chosen replacement is the connected animation on the Snippets page (see P11.5).

### F3 — About page has an empty slot (P10.0, already planned)
`brand/hero.svg` doesn't exist; the page is a title, two paragraphs, a link and an expander over a large empty area. This is the already-planned hero illustration; it also feeds the README and installer.

### F4 — Radius hierarchy not applied
DESIGN.md says window 8 / panels 6 / controls 4. In the build everything is 4: Settings cards, the list selection, the welcome window content. The result is uniformly "boxy". The window's own rounding is the system default.

### F5 — The tray menu is stock WPF chrome
The tray menu is the *most-seen surface* of the app (the window is opened rarely), and it is an unstyled `ContextMenu`: light grey on a dark taskbar, squared off, clearly not Fluent. This one surface does more for "is this a real app" than any page in the window.

### F6 — Errors and cautions share one colour
`SnippetsPage` renders both validation errors ("already used by…", "use letters, numbers…") and the caution ("very long snippet…") in Ochre. The view model already distinguishes them (`ShortcutMessageIsError`); the XAML doesn't act on it. Errors should read as errors (Fluent's critical fill), cautions stay Ochre.

### F7 — The Settings "Theme" row from DESIGN.md §8 / UX_COPY (`Settings.Theme`) isn't implemented
Theme follows the OS only. The copy and the sketch both include `Theme  System ▾`. Either build the row or strike it from the docs — an undocumented gap like this is how specs rot.

### F8 — Light theme, high contrast and DPI scaling never verified
Every capture to date is dark theme at (presumably) 100–150 %. P10.2's checklist covers this; no code change expected, but the review can't sign off what it hasn't seen.

### F9 — Ember has no job in the shipping app
By design: Ember was "the dot in the progress pill, the download bar" — both parked. Keep it reserved in the palette (the AI feature will want it back); do **not** invent a use for it.

### F10 — Housekeeping
`ui-check-*.png` files are checked into git although the capture script says outputs are ignored; `check-screens.ps1` still loops over the parked "AI actions"/"Offline AI" pages.

---

## The plan: Phase P11 — Craft pass (before P10.2)

Numbered as a new phase so P10 stays the release record. Each task follows the AGENTS.md workflow (`dotnet build` + `dotnet test` green, tick, commit as `P11.x: …`). Total scope is deliberately small: no new dependencies, no new pages, no new copy beyond two decision-doc updates. **Per-task implementation detail (files, code sketches, acceptance criteria, risks) is in [`P11_CRAFT_PASS.md`](P11_CRAFT_PASS.md).**

### P11.0 Decisions into the docs first (no code)
Append to DESIGN.md: (a) a **motion** sub-section — durations (150–300 ms), easing (decelerate `cubic-bezier(0,0,0,1)` entrances, accelerate exits), the one-pattern rule (entrances only; nothing loops), the reduced-motion gate (`SystemParameters.ClientAreaAnimation`); (b) radius application: add a `PanelRadius="6"` for cards/selections, controls stay 4, window stays system; (c) error colour: Fluent critical fill for errors, Ochre stays caution-only; (d) tray menu theming. Four short maintainer decisions (D7 motion, D8 radius, D9 error colour, D10 tray menu) recorded like D1–D6 were.
*Done when:* DESIGN.md carries the decisions; nothing else changed.

### P11.1 Theme tokens and error colour
Add `PanelRadius` resource, apply to `CardControl`s and the list-selection template. Add `ErrorBrush` (system critical fill, both themes) and bind the shortcut message colour to `ShortcutMessageIsError`. Tests: none beyond build; visual check via `check-screens.ps1`.

### P11.2 Settings "Theme" row (System / Light / Dark)
A `CardControl` row in the "Wordwright" group using the existing `Settings.Theme` copy; persists to `settings.json`; applied via the existing accent-swap code path in `App.xaml.cs` with the app watching for OS changes only in "System". Core test for the persisted value.
*Done when:* switching to Light re-skins the window immediately; restart keeps the choice.

### P11.3 Tray menu theming
Restyle the tray `ContextMenu` with a Fluent-styled template (palette brushes, 4 px radius, taskbar-theme-following light/dark using the same `SystemTheme` logic as the tray icon). No new dependency.
*Done when:* `scripts/check-screens.ps1`-style capture (or human check) shows the menu matching the taskbar theme in both modes.

### P11.4 The motion helper
`Motion.cs` in Wordwright.App: one entrance-animation factory (fade + 8 px slide, 200 ms decelerate), one exit (fade, 120 ms accelerate), all no-ops when Windows animations are off. Everything in P11.5–P11.6 goes through it, so motion stays one system, not scattered storyboards.

### P11.5 Signature move: connected animation, list → editor
Selecting a snippet animates a proxy of the shortcut chip from the tapped list row into the editor's Shortcut field (200 ms, decelerate), while the editor fields settle in one choreographed stagger. Also through the helper: page-entrance transition on navigation, "Saved" fade in/out, list add/remove transitions. One pattern, everywhere-gated by reduced motion.
*Done when:* with animations off, the app is pixel-identical in behaviour to today; with them on, selection, navigation and save all move once and stop.

### P11.6 Playground completion moment
In the welcome window and the Snippets empty state, the first successful expansion reveals `Welcome.TryHere.Done` with the entrance animation and a small accent check mark. This is the flagship interaction getting its payoff; copy already exists.

### P11.7 Housekeeping
Drop the parked "AI actions"/"Offline AI" pages from `check-screens.ps1`; gitignore `scripts/ui-check-*.png` and untrack the checked-in captures.

The hero illustration stays **P10.0** (generated and recoloured by hand per `brand/HERO_BRIEF.md`; it fills the About slot during this pass), and the full light / dark / high-contrast / DPI verify is **P10.2**, which now runs after the craft pass.

## Decisions for the maintainer — **all five approved 2026-10-02** and recorded in PLAN.md / DESIGN.md

| # | Question | Recommendation |
|---|---|---|
| D7 | Adopt the connected animation as the shipping app's signature move (replacing the parked time ruler)? | Yes — it's Fluent's own gesture, it answers "basic" with motion instead of decoration, and nothing in the niche does it. |
| D8 | Apply the 6 px panel radius from DESIGN.md? | Yes — it was specified, just never applied. |
| D9 | True errors get the system critical red, cautions keep Ochre? | Yes — matches Fluent and the VM already knows the difference. |
| D10 | Theme the tray menu (dark/light, Fluent)? | Yes — it's the most-seen surface and the only remaining stock chrome. |
| D11 | Build the Settings "Theme" row, or remove it from the docs? | Build it — the copy exists, users on light-taskbar systems will want it. |

Once D7–D11 are confirmed, P11.0 records them and the rest is mechanical. Nothing here breaks the hard rules: no network, no logging of content, no new dependencies, all strings already in `UX_COPY.md`.
