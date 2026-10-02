# Craft-studio pass, 2026-10-03 (after P13.1–P13.15 and P10.5)

The `craft-studio` playbook (`playbooks/desktop-app.md`) run against the installed
1.0.0 build, with every source actually opened in the browser pane this time. The
captures are `scripts/ui-check-*.png` (current size and 800×600, all three pages,
0 controls without an automation name on any of them).

## Study note

- **Medium / platform:** Windows 11 desktop app, WPF + WPF-UI (Fluent, Mica), tray-resident. Playbook: desktop-app.
- **What it is:** a private text expander for Windows. Type `;sig` and it becomes the paragraph you would otherwise type again, in any app.
- **Who, where, how long:** non-technical people who write all day. The window is opened rarely; the daily surface is the expansion and the tray icon.
- **Keep (seen in the build):** Forge ink / Ink light accent, Zodiak titles over Segoe UI Variable, Phosphor Regular, Mica, the left rail with the two-line wordmark, split list/editor, the quiet "Inserts as:" preview line, the accent-coloured GitHub link, the hero on About.
- **Platform conventions to honour (numbers taken today):** 4 px grid; 8 px between buttons, 12 px label to control, 16 px surface edge to text, 24 px content margins (Learn, content basics); page header 52 px; top-level page transitions are a quick fade with no slide (Fluent 2 motion); settings pages are a `BodyStrong` section header with 30 px above and 6 px below, cards 4 px apart (WinUI 3 Gallery source).
- **Niche references:** Text Blaze, Beeftext, Espanso → shared: tray-resident, keyword-replaces-itself demo, list left and editor right. Nobody does: no network at all (Beeftext admits a daily update check on its own home page), no cap, a native Fluent window, a first run you try inside the app.
- **What is unique:** private by construction, and you can check it.
- **Signature moves considered:** (3) connected animation list → editor (already built), (6) the empty state is the first task (already built, the playground), (7) ambient tray status (D19/D24, open). **Chosen:** keep (3) and (6); nothing new is added, since a second flagship would dilute the first.
- **Palette / type seed:** unchanged (DESIGN.md).
- **Open questions:** whether Settings and About move to the pane footer (finding 3 below); the maintainer decides.

## Sources visited

| Source (URL) | Looked at | Taken for this work | Not taken, because |
|---|---|---|---|
| fluent2.microsoft.design/layout | Global spacing ramp, grid anatomy | the 4 px ramp with 2/6/10 exceptions for icon padding; the 44 px touch-target minimum | the 12-column grid: a fixed two-column app page does not need it |
| fluent2.microsoft.design/motion | Duration and easing; transitions; choreography | **"Top level" rule: page-to-page is a quick fade, no slide** → `Motion.EnterPage`, finding 1; staggering only for in-page sets | container transform and elevation patterns: no resizing surfaces in the app |
| learn.microsoft.com/windows/apps/design/basics/content-basics | Spacing and gutters, text hierarchy, lists | 8 px between buttons (Insert row and Export/Import already use 8), 12 px label-to-control, 24 px content margins, Body + Caption for two-line list rows (D20) | the 48 px indent inside expanders: no nested settings here |
| learn.microsoft.com/windows/apps/develop/ui/controls/navigationview | Pane anatomy, footer items, header | the convention that Settings sits at the end of the pane (`FooterMenuItems`), finding 3; header height 52 px | top navigation: three items on the left rail is right for a window this shape |
| learn.microsoft.com/windows/apps/develop/ui/controls/menus-and-context-menus | When a context menu vs a menu | confirms the tray menu is a context menu with secondary commands, so the three-row layout stands | command-bar flyout: no Cut/Copy/Paste commands in the tray |
| github.com/microsoft/WinUI-Gallery … /Pages/SettingsPage.xaml | The Gallery's own Settings page | `SettingsCardSpacing` 4 px; section header margin 1,30,0,6; 36 px page padding; `MaxWidth` 1064; `RepositionThemeTransition` on the list → finding 2 | putting About inside Settings as an expander: Wordwright's About carries the hero and the privacy statement and earns its own page |
| blaze.today | Home page, how-it-works | the three-step "save text, give it a shortcut, type it anywhere" framing for the README order (P10.1) | the red SaaS look, star ratings, mascot |
| beeftext.org | Why, open source, privacy sections | its honesty line about the daily update check makes "no network at all" a stated differentiator worth keeping on About and the Store listing | its plain Qt windows: nothing to borrow visually |
| espanso.org | Hero, how it works, search bar | **the hero mechanism: the keyword is typed, then replaced in place** → this is the README GIF and the Store screenshot (P10.1, P13.11); Alt+Space picker confirms D14's shape | YAML configuration, packages, shell scripts |
| tympanus.net/codrops (search: list detail transition) | 185 results, first page | nothing | every result is a web case study with GSAP/WebGL; the app's connected animation is already in `Motion.cs` |
| uiverse.io (search: toast) | Toast elements | nothing beyond the D23 undo-line idea, which the plan already holds | plain CSS/Tailwind snippets; a WPF app takes the mechanism, not the code |
| WinUI 3 Gallery (Store app) | — | — | **not opened**: the Store app cannot be installed from this session (installs are redirected); its source on GitHub was read instead |

## Findings and what was done

1. **Page navigation slid as well as faded (done).** `Motion.Enter` (fade plus 8 px rise) ran on every page `Loaded`. Fluent 2's motion page says top-level transitions use a quick fade only, because the element is large. `Motion.EnterPage` now fades the page (200 ms, decelerate, no transform), the three pages use it, and the rise stays for in-page elements ("Saved", list rows, the playground). DESIGN.md's Motion section says so. Build 0 warnings, 192 tests.
2. **Settings spacing differs from the Gallery's (open, P13.16).** Section headers are 24 px above / 8 px below, the Gallery uses 30 / 6; the lower cards (Theme, Start with Windows) are 8 px apart where the excluded-app cards and the Gallery use 4. One pass to align both, then recapture.
3. **Settings is a menu item, not a pane-footer item (decision, D26).** The NavigationView guidance places Settings at the end of the pane. With three items the current order reads fine, so this is the maintainer's call; the change is `FooterMenuItems` for Settings and About, no new copy.
4. **At 800×600 the editor hides its tail (open, P13.17).** The Insert row wraps to two lines and the Delete button sits below the fold; the column scrolls, but a first-time user at the minimum size does not see Delete. Either tighten the body box minimum (180 px today) at small heights or move Delete beside New snippet.
5. **The README GIF has its mechanism (feeds P10.1 / P13.11).** Espanso's hero is the exact shot: keyword typed, replaced in place. Wordwright's version is the welcome playground mid-expansion, which no competitor shows.

Everything else on the playbook's verify list that can run from this session held: Mica, the type ramp, 32 px controls, 4/6/8 radii, zero unnamed controls, reduced motion as a no-op, remembered window state, 800×600 minimum enforced. Light theme, 150/200 %, high contrast, Narrator and launch time remain the maintainer's checks in `RELEASE_CHECKLIST.md`.
