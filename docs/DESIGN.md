# Design

> **Partly parked.** The AI screens — the consent dialogue (§5), the action palette
> (§6) and the progress pill (§7) — belong to the on-device AI writing assistant,
> which is **not part of the application that ships**. They are kept for the record;
> the palette in particular is preserved on the `ai-rewriting` tag. See
> [`AI_REWRITING.md`](AI_REWRITING.md). Everything else here describes the app.


Follows the maintainer's `craft-studio` playbook (`playbooks/desktop-app.md`): study the product and its niche first, two-pass direction, one signature move, the 13 anti-slop rules read for an app, free resources only.

## Study note (craft-studio §0, 2026-10-01, build after P3.4; rewritten for the snippets-only app on 2026-10-03, P13.13)

- **Medium / platform:** Windows 11 desktop app, WPF + WPF-UI (Fluent, Mica), tray-resident. Playbook: desktop-app.
- **What it is:** a private text expander for Windows. Type a short shortcut such as `;sig` and it becomes the text you would otherwise type again and again, in any app. A wright makes things that work; Wordwright makes a few keystrokes do the work of a paragraph, and keeps everything on your own PC.
- **Who, where, how long:** non-technical people who write all day at work. The window is opened rarely (set up snippets, change a setting); the daily surface is the expansion itself, inside other apps, and the tray icon. They must trust it with their keyboard and their clipboard.
- **Keep (verified in the running build):** Forge ink / Ink light accent, Zodiak + Segoe UI Variable, Phosphor Regular, Mica, the left rail with the two-line wordmark, the split list/editor on Snippets, the plain voice of the copy.
- **Platform conventions to honour:** grouped settings rows (Fluent), a minimum window size and remembered size, keyboard accelerators, UI Automation names, high-contrast themes, 100–200 % scaling, no marketing styling inside the app.
- **Niche references:** Text Blaze (the closest on the Windows Store: cloud-synced, account required, free plan capped at 20 snippets of 2,500 characters), Beeftext (offline and free, with a plain utilitarian window), TextExpander and PhraseExpress (paid, account or licence, built for teams), Espanso (offline and open source, but configured in YAML files). **Shared:** tray-resident; list left, editor right; a prefix character (`/`, `;`, `:`); seeded example snippets; date, time and clipboard variables; a picker hotkey in most. **Nobody does all of:** no account and no network connection at all; no cap on snippet count or length; a native Fluent window a non-technical person is comfortable in; a first run you can *try inside the app* (Espanso's welcome window comes closest); the clipboard put back after every paste and kept out of Win+V history; privacy stated plainly as the headline. Gaps Wordwright accepts for v1: Text Blaze's forms and formulas (fill-in fields, the D22 Pro candidate) and a picker hotkey (D14).
- **What is unique (the design hook):** *private by construction, and you can check it.* There is no connection to find in Resource Monitor, no account to make, and your data is one folder the app opens for you; the welcome window proves expansion works before you leave it, inside the app. Every claim on the About page is one the user can verify on their own PC.
- **Candidate signature moves (desktop playbook menu):** (1) *progressive onboarding empty state*: a "Try it here" box on the welcome window and the empty Snippets page where typing `;date` expands in place; (2) *ambient status*: the progress pill shows a hairline ruler that fills against the promised time, so every rewrite echoes the signature screen; (3) *command palette*: the action palette, already planned. **Chosen:** the "Try it here" playground is the flagship interaction of the window, because the window's only job is to make a non-technical person trust the app in thirty seconds (approved 2026-10-01, PLAN.md P3.4c). Moves (2) and (3), and the time ruler, left with the AI feature; the shipping app's one motion signature is the connected animation from the list to the editor (*Motion (shipping app)* below).
- **Found in the build:** no minimum window size and no remembered size; no keyboard accelerators; Settings is a flat list with Remove buttons far from their rows; About uses the system-blue hyperlink (rule 7 of AGENTS.md) and shows no version; 4 of 5 interactive controls on About have no automation name (confirmed by automation id: WPF-UI's three title-bar buttons and the navigation toggle). DESIGN.md had no sketch for Settings or About; §8 and §9 below add them.
- **Open questions:** none on the font: the Zodiak licence (ITF FFL 2.0) allows embedding in the app (§01) and forbids redistributing the files through a repository (§02), so the build fetches them (docs/PLAN.md P13.12). The welcome playground is also the empty state of the Snippets list (decided with D1).

### Text Blaze, looked at closely (2026-10-01)
Design: a web dashboard in red/white with a panda mascot and feature GIFs; folders tree, snippet list, editor, and a searchable command menu on the right. Generic SaaS; nothing to borrow in look, and the red is the opposite of Wordwright's calm ink. Mechanisms worth taking, all four approved on 2026-10-01 (PLAN.md P2.7, P3.4c, P6.6, P6.8):
- **Typing-delay guard (D3).** A shortcut typed with a long pause in the middle does not expand ("type half, make a cup of tea, type the rest"). Prevents accidental expansions; Wordwright's buffer has no time rule today.
- **Live preview of variables in the editor (D4).** Text Blaze has a Preview / Try it out button that renders dynamic commands. Wordwright can show the expanded text (`{date}`, `{time}` resolved, `{cursor}` marked) in a quiet line under the Text box, live, with no button.
- **Copy as the fallback when paste can't happen (D5).** AI Blaze copies the result to the clipboard when no text box is active. Wordwright's elevated-app case could copy the rewrite and say so, instead of only reporting the failure.
- **Snippet picker in the palette (D6, v1.1 candidate).** Text Blaze's right-click list of all snippets, and every Windows expander's picker hotkey: the palette lists snippets too (type to filter, Enter inserts). Pulled into v1; sketch in §6.
- Noted, no change: case-insensitive shortcuts by default (Wordwright is case-sensitive; revisit if users hit it); trigger-mode per folder (Wordwright's word-boundary rule already matches the default); forms and formulas (out of scope for v1); descriptive dotted shortcuts like `/english.grade` (the help text under Shortcut already explains the rule).

## Pass 1: direction

- **Subject.** A *wright* is a maker who shapes raw material into something that works. Wordwright turns a few keystrokes into finished text, in any app, on your own PC.
- **Audience.** People who write all day at work and are not technical. They must trust it with their keyboard and their clipboard.
- **Primary job.** Be invisible while typing and instant when a shortcut is typed; in the window, make a snippet in seconds and make the privacy promise easy to believe.
- **Signature moment (spend boldness only here).** The first expansion in the welcome window's playground: the user types `;date` and watches it become today's date, inside the app, before trusting it anywhere else. The window's one motion signature is the connected animation from the snippet list to the editor (*Motion (shipping app)* below). *(Until 2026-10-03 this was the AI feature's time ruler, parked with it; see AI_REWRITING.md.)*
- **Motion idea (parked with the ruler).** A stroke being drawn: left to right, ease-out, once.

### Palette (named, tinted toward ink; no flat greys)
| Name | Hex | Role |
|---|---|---|
| Forge ink | `#23408E` | Accent in light theme: primary buttons, focus ring, selection, ruler strokes, icon tile |
| Ink light | `#A4B6F0` | Accent in dark theme |
| Steel | `#E9ECF3` | Tinted neutral: panels and list selection in light theme |
| Anvil | `#1A2030` | Tinted near-black: body text in light theme, panels in dark theme |
| Ember | `#C7621E` | Used only for "working" states: the dot in the progress pill while rewriting, the live download bar |
| Ochre | `#9A5B00` (dark theme `#E8B45A`) | Caution notes: "may be slow on this PC", low disk or memory, the "very long snippet" warning |
| Errors | system critical fill (WPF-UI `SystemFillColorCriticalBrush`) | Validation errors only: "shortcut already used", "invalid characters" (decision D9, 2026-10-02). Cautions stay Ochre |

Ember is reserved: its two jobs (the progress pill, the download bar) are parked with the AI feature, and nothing in the snippets-only app uses it (2026-10-02 review, finding F9).

Window backgrounds use the Windows 11 Mica material. No gradients anywhere.

### Type (at most two families, clearly different)
| Role | Face | Source | Use |
|---|---|---|---|
| Display | **Zodiak** (Semibold, Bold) | Fontshare (ITF Free Font License, free for commercial use; embedding in an app is allowed; the files are fetched at build time, not committed (P13.12)) | Wordmark, page titles, dialogue titles, the big numbers on the time ruler |
| Text | Segoe UI Variable Text | Built into Windows | All body text, labels, buttons, inputs |

Why: a sharp, crafted serif for the maker's voice; the system face for everything the user reads and clicks, so the controls still feel native. Embed the Zodiak `.otf` files as WPF resources.

Scale (px, modular ~1.25): 32 wordmark / 26 page title / 20 dialogue title / 14 body / 12 secondary. Sentence case everywhere. No all-caps labels, no single-word accent in titles, tabular figures for numbers.

### Layout
Left-aligned throughout. Structure varies by screen (split list/editor, a single-column dialogue, a floating palette) rather than a grid of identical cards. Corner radius follows hierarchy: window 8 (system), panels 6, controls 4, the pill fully rounded. Cards and the snippet-list selection take the 6 px panel radius; fields, buttons and menu items stay 4 px (decision D8, 2026-10-02). System shadows only.

## Motion (shipping app — decision D7, 2026-10-02)

The time ruler's drawn stroke was the motion idea, and it is parked with the AI feature. The shipping app gets **one system, one pattern** instead, with the connected animation as its flagship (desktop playbook menu item 3):

- **Signature move:** selecting a snippet glides a proxy of its shortcut chip from the list row into the editor's Shortcut field (200 ms, decelerate), while the editor fields settle in a single choreographed stagger.
- **Entrances** ("Saved", playground completion, list add): fade plus an 8 px slide, 200 ms, decelerate `cubic-bezier(0,0,0,1)`. Page navigation fades only, no slide: Fluent 2's top-level transition rule for large elements (craft pass, 2026-10-03).
- **Exits** (list remove, dismissed indicators): fade, 120 ms, accelerate.
- Nothing loops and nothing animates on a timer; motion responds to the user's own actions only.
- Everything goes through one helper (`Motion.cs`) and is a strict no-op when Windows animations are off (`SystemParameters.ClientAreaAnimation`), honouring reduced motion. With animations off, behaviour is identical to having no motion code at all.

## Pass 2: rejected defaults
| First instinct | Why rejected | Replaced with |
|---|---|---|
| Segoe UI for everything | Anti-slop rule 1: system font as display face | Zodiak for display, Segoe only for text |
| Pen with sparkle stars in the logo | The most common AI-logo cliché | A "W" whose two points are pen nibs |
| Cream background with a warm accent | Anti-slop rule 3 palette cliché | Mica + tinted steel/anvil neutrals, forge-ink accent |
| Fade-in on every panel | Anti-slop rule 9 | One drawn stroke on the time ruler; everything else responds only to actions |
| Emoji or mixed icon styles | Anti-slop rule 6 | Phosphor icons (Regular weight) only, MIT licence |

## Logo system (two tiers)
Files in `brand/`.
- **Mark:** a bold "W" whose two lower points are pen nibs with slits. It reads as a letter at 16 px and as two pens at full size. `mark.svg` (ink), `tray-light-taskbar.svg` (anvil), `tray-dark-taskbar.svg` (white).
- **App icon:** the mark in white on a forge-ink rounded tile. `icon.svg`. Export `.ico` at 16, 20, 24, 32, 48, 64, 256 px.
- **Wordmark:** "Wordwright" in Zodiak Semibold, sentence case, forge ink, the mark to its left at cap height.
- **Hero illustration (README, installer, About page):** see `brand/HERO_BRIEF.md`. `brand/hero.svg` was drawn by hand to the brief as a few flat shapes rather than generated (P10.0), so it needs no recolouring. The app draws it from `Resources/Hero.xaml` (same shapes; the ink follows the theme accent and the hand is cut from white or Anvil), and `scripts/IconGen --hero` renders that file to the installer splash.

## Screens

### 1. Tray menu
```
Open Wordwright
Snippets on                    ✓
───────────────────────────────
Quit Wordwright
```
(The "Offline AI on" row is parked with the AI feature.) Styled to Fluent: palette brushes, 8 px outer radius (it is a window unto itself) and 4 px items, light or dark following the taskbar theme through the same `SystemTheme` check as the tray icon (decision D10, 2026-10-02). The tray menu is the most-seen surface of the app — the window is opened rarely — so stock WPF chrome is not acceptable here. On Windows 11 22H2 and later it sits on the transient Acrylic material like the system's own menus (P13.14): a 60 % Anvil or Steel tint over the backdrop, with Windows drawing the corners, border and shadow; on older builds it stays solid. An Ember dot on the tray icon means a better model is available (parked with the AI feature).

### 2. Main window (NavigationView, left rail)
```
┌─────────────┬──────────────────────────────────────────────┐
│ [W] Word-   │ Snippets                        [New snippet] │
│     wright  │ ┌──────────────────┐ ┌─────────────────────┐ │
│ Snippets    │ │ Search snippets  │ │ Name                 │ │
│ AI actions  │ │──────────────────│ │ Shortcut   ;sig      │ │
│ Offline AI  │ │ ;sig   Email sig │ │ Text                 │ │
│ Settings    │ │ ;addr  Office    │ │ ┌─────────────────┐  │ │
│             │ │ ;ty    Thank you │ │ │ Best regards,   │  │ │
│             │ └──────────────────┘ │ └─────────────────┘  │ │
│ About       │                      │ Insert: Date Time …  │ │
└─────────────┴──────────────────────────────────────────────┘
```
Page titles in Zodiak. Auto-save with a quiet "Saved" beside the title.

### 3. AI actions page
Same split layout. Editor fields: Name, Letter (palette shortcut), **Hotkey** (a hotkey recorder: click, press the combination, shows it or the "already in use" error), Instruction, Enabled. A "Try it" box below.

### 4. Offline AI page
Off: one paragraph and the primary button. On: active model panel with measured times, "Check for a better model", "Change model", "Import model file", weekly-check toggle; better-model banner on top when available.

### 5. Turn on offline AI: the signature screen (560 px dialogue)
```
┌──────────────────────────────────────────────────────┐
│ Recommended for your PC                (Zodiak 20)    │
│                                                       │
│ Qwen3.5 2B                                            │
│ 1.6 GB download, Apache-2.0 licence                   │
│ Your PC: 16 GB memory, Intel Core i5-1235U,           │
│ no graphics card suitable for AI                      │
│                                                       │
│  0s    2    4    6    8    10   12   14   16          │
│  |────|────|────|────|────|────|────|────|            │
│  ████████                      One line: 2–4 s        │
│        ██████████████████      Short paragraph: 4–9 s │
│                                                       │
│ Good at: grammar and spelling, shortening, tone       │
│ Not so good at: long paragraphs, translation          │
│ The first rewrite after starting your PC takes about  │
│ 5 seconds longer while the model loads.               │
│                                                       │
│ Show other options                                    │
│              [Not now]   [Download and turn on]       │
└──────────────────────────────────────────────────────┘
```
Each estimate: a Steel band for the range with a Forge-ink stroke along it, drawn left-to-right once (400 ms ease-out; skipped when Windows animations are off). The ruler scales to the slowest estimate, max 30 s (beyond that "30+" and an Ochre note). After calibration the same ruler redraws with single measured strokes: the moment the promise becomes real.

### 6. Action palette (floating, 360 px, Fluent flyout material)
```
┌─────────────────────────────────────────┐
│ Type to filter…                          │
│─────────────────────────────────────────│
│ Fix grammar and spelling   G  Ctrl+Alt+G │
│ Make it clearer            C             │
│ More formal                F             │
│ More friendly              R             │
│ Shorten                    S             │
│ Custom instruction…        I             │
│─────────────────────────────────────────│
│ Snippets                                 │
│ Email signature            ;sig          │
│ Thanks                     ;thanks       │
└─────────────────────────────────────────┘
```
Right column shows the letter and, if set, the action's own hotkey, so users learn the direct shortcuts. The Snippets group (D6) lists enabled snippets with their full shortcut in the right column; Enter inserts at the caret. With nothing selected and AI off, the palette opens on this group.

### 7. Progress pill (floating, 32 px tall, never takes focus)
Ember dot + "Rewriting… 3 s   Esc to cancel" → check mark + "Done. Ctrl+Z undoes it." (3 s). Short errors use the same pill. Under the text, a 1 px Steel track with an accent fill that grows over the expected time for this input (the ruler tick, decision D2); past the estimate it continues in Ember. Skipped when Windows animations are off.

### 8. Settings page (grouped Fluent rows; each row is a WPF-UI `CardControl`: label and help text left, control right)
```
Settings
Snippets
  Snippet prefix                                             [ ;  ]
  Turn Wordwright off in these apps
  Wordwright can't always tell when you're typing a password…
    KeePass.exe                                             [Remove]
    KeePassXC.exe                                           [Remove]   ← Remove sits inside the row, 8 px from the name
  [Add app]
  Export snippets   Import snippets
AI                                      (from P6: Hotkey for all AI actions [recorder], per-action hotkeys note)
Wordwright
  Start Wordwright when I sign in                            (toggle)
  Theme                                                      System ▾
  Check weekly for Wordwright updates                        (toggle, P8)
  Open data folder
```
Group headings in Segoe UI Variable Semibold 14, not Zodiak (Zodiak is for page titles only). Card rows take the 6 px panel radius (decision D8); no card-in-card.

### 9. About page
```
[hero.svg, P10.0, max 320 px wide, left-aligned]
Wordwright                     (Zodiak 26)
by Unbound Kite                (body; the publisher, 2026-10-03)
Version 1.0.0                  (secondary)
About.Body
About.Privacy
View source on GitHub          (accent colour, not the system blue)
Support Wordwright             (D12: the Razorpay Payment Page; opens the browser, the app itself stays offline; hidden until the page exists)
Third-party licences ▸         (Zodiak, Phosphor, WPF-UI, H.NotifyIcon, Velopack; expands in place)
```

## Polish checklist (run before each release, craft-studio §7 and the desktop playbook's verify list)
Hierarchy and spacing; type scale respected; AA contrast in light, dark **and a Windows high-contrast theme**; visible focus everywhere; keyboard-only use of every screen; automation names for screen readers (`scripts/check-screens.ps1` counts unnamed controls; Accessibility Insights for Windows for the full pass); **100 %, 150 % and 200 % scaling; 800×600 and maximised**; launch to tray under 1 s; empty states invite action; Windows reduced-motion and text-size honoured; no leftover placeholder text; anti-slop list re-checked with the app readings (no hero, no marketing styling, texture is Mica).
