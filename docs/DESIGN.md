# Design

Follows the maintainer's `craft-studio` playbook: two-pass direction, the 13 anti-slop rules, free resources only.

## Pass 1: direction

- **Subject.** A *wright* is a maker who shapes raw material into something that works. Wordwright takes rough text and shapes it into clean text, in any app, on your own PC.
- **Audience.** People who write all day at work and are not technical. They must trust it with their keyboard.
- **Primary job.** Be invisible while typing, fast when called, and completely clear at the one moment that needs a decision: turning on offline AI.
- **Signature moment (spend boldness only here).** The *time ruler* in the "Turn on offline AI" dialogue: estimated rewrite times drawn as forged ink strokes on a ruler, and redrawn with the real measured times after calibration.
- **Motion idea.** A stroke being drawn: left to right, ease-out, once.

### Palette (named, tinted toward ink; no flat greys)
| Name | Hex | Role |
|---|---|---|
| Forge ink | `#23408E` | Accent in light theme: primary buttons, focus ring, selection, ruler strokes, icon tile |
| Ink light | `#A4B6F0` | Accent in dark theme |
| Steel | `#E9ECF3` | Tinted neutral: panels and list selection in light theme |
| Anvil | `#1A2030` | Tinted near-black: body text in light theme, panels in dark theme |
| Ember | `#C7621E` | Used only for "working" states: the dot in the progress pill while rewriting, the live download bar |
| Ochre | `#9A5B00` (dark theme `#E8B45A`) | Caution notes: "may be slow on this PC", low disk or memory |

Window backgrounds use the Windows 11 Mica material. No gradients anywhere.

### Type (at most two families, clearly different)
| Role | Face | Source | Use |
|---|---|---|---|
| Display | **Zodiak** (Semibold, Bold) | Fontshare (ITF Free Font License, free for commercial use; confirm the licence allows embedding in an app before release, else fall back to Erode) | Wordmark, page titles, dialogue titles, the big numbers on the time ruler |
| Text | Segoe UI Variable Text | Built into Windows | All body text, labels, buttons, inputs |

Why: a sharp, crafted serif for the maker's voice; the system face for everything the user reads and clicks, so the controls still feel native. Embed the Zodiak `.otf` files as WPF resources.

Scale (px, modular ~1.25): 32 wordmark / 26 page title / 20 dialogue title / 14 body / 12 secondary. Sentence case everywhere. No all-caps labels, no single-word accent in titles, tabular figures for numbers.

### Layout
Left-aligned throughout. Structure varies by screen (split list/editor, a single-column dialogue, a floating palette) rather than a grid of identical cards. Corner radius follows hierarchy: window 8 (system), panels 6, controls 4, the pill fully rounded. System shadows only.

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
- **Hero illustration (README, installer, About page):** see `brand/HERO_BRIEF.md`. Generated with a free tool, then simplified to the palette.

## Screens

### 1. Tray menu
```
Open Wordwright
Snippets on                    ✓
Offline AI on                  ✓   (or "Turn on offline AI…")
───────────────────────────────
Quit Wordwright
```
An Ember dot on the tray icon means a better model is available.

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
└─────────────────────────────────────────┘
```
Right column shows the letter and, if set, the action's own hotkey, so users learn the direct shortcuts.

### 7. Progress pill (floating, 32 px tall, never takes focus)
Ember dot + "Rewriting… 3 s   Esc to cancel" → check mark + "Done. Ctrl+Z undoes it." (3 s). Short errors use the same pill.

## Polish checklist (run before each release, craft-studio §7)
Hierarchy and spacing; type scale respected; AA contrast in light and dark; visible focus everywhere; keyboard-only use of every screen; automation names for screen readers; empty states invite action; Windows reduced-motion and text-size honoured; no leftover placeholder text; anti-slop list re-checked.
