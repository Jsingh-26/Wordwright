# Phase P11 — Craft pass: detailed implementation plan

> **Historical.** The implementation plan for Phase P11, which is complete. The current design is in [DESIGN.md](DESIGN.md) and the plan in [PLAN.md](PLAN.md).

Companion to the checklist in `docs/PLAN.md` (Phase P11) and the reasoning in `docs/UI_REVIEW.md`. This is the *how*: exact files, code sketches, acceptance criteria and risks, one task at a time, in order. All decisions (D7–D11) were approved 2026-10-02 and are recorded in DESIGN.md.

Guardrails that apply to every task below (AGENTS.md hard rules): no network, no user text to disk or logs, nothing new written inline — all strings already exist in `UX_COPY.md`/`Strings.resx`, no new colours or fonts, no new NuGet dependencies. If any sketch below turns out to need one of those, stop and ask.

Run order: **P11.1 → P11.2 → P11.3 → P11.4 → P11.5 → P11.6 → P11.7**, then P10.0 (hero, by hand), P10.1 (README/GIF — last, so the GIF shows the final UI), P10.2 verification.

---

## P11.1 Theme tokens — radius split and error colour

**Files:** `src/Wordwright.App/Resources/Theme.xaml`, `src/Wordwright.App/Pages/SnippetsPage.xaml`, `src/Wordwright.App/Pages/SettingsPage.xaml`, `src/Wordwright.App/Pages/AboutPage.xaml`.

**1. Radius resource.** `Theme.xaml`, after the brushes:

```xml
<!-- Shape (docs/DESIGN.md, D8): panels 6, controls 4. -->
<CornerRadius x:Key="PanelRadius">6</CornerRadius>
```

Apply `{StaticResource PanelRadius}` to:
- `SnippetsPage.xaml` — the ListBoxItem template's `Row` Border (currently `CornerRadius="4"`).
- `SettingsPage.xaml` — every `ui:CardControl` (currently `CornerRadius="4"`).
- `AboutPage.xaml` — the licences `ui:CardExpander`.

Controls (text boxes, buttons, the prefix box) stay 4 px.

**2. Error colour (D9).** `SnippetsPage.xaml` — the shortcut message `TextBlock` gets a style with a data trigger; the VM already exposes `ShortcutMessageIsError`:

```xml
<TextBlock Margin="2,4,0,0" TextWrapping="Wrap"
           Text="{Binding ShortcutMessage}"
           Visibility="{Binding HasShortcutMessage, Converter={StaticResource BoolToVisibility}}">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="{DynamicResource CautionBrush}" />
            <Style.Triggers>
                <!-- D9: true errors read as errors; cautions ("very long…") stay Ochre.
                     SystemFillColorCriticalBrush is WPF-UI's theme-following critical fill. -->
                <DataTrigger Binding="{Binding ShortcutMessageIsError}" Value="True">
                    <Setter Property="Foreground" Value="{DynamicResource SystemFillColorCriticalBrush}" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

The body message (`BodyMessage`) stays unconditionally `CautionBrush` — the only body message today is the very-long caution.

**Acceptance:** `dotnet build` + `dotnet test` green; `scripts/check-screens.ps1` capture shows cards/selection at 6 px and controls still 4 px; typing a duplicate shortcut renders the red critical fill while a >1,000-character snippet keeps the Ochre caution.

**Risks:** (a) if `SystemFillColorCriticalBrush` is absent from WPF-UI 4.3's theme dictionaries, the `DynamicResource` silently no-ops and errors fall back to Ochre — acceptable failure, but confirm visually once with a deliberately duplicated shortcut; (b) WPF-UI `CardControl` radius is a plain property, so `StaticResource` is safe — but verify no template re-hardcodes 4 internally (check the capture).

---

## P11.2 Settings "Theme" row

**Copy:** `Settings.Theme` ("Theme") already exists in `Strings.resx` and UX_COPY.md. The option labels are new-but-trivial strings; if resx additions are wanted, add `Settings.Theme.System/Light/Dark` to UX_COPY.md first (one new table row each), then mirror them in resx — follow the house rule, don't hardcode.

**Files:** `src/Wordwright.App/Pages/SettingsPage.xaml` + its view model, `src/Wordwright.Core/Settings/SettingsStore.cs` (add `Theme` to the settings object, default `"System"`, atomic save unchanged), `tests/Wordwright.Core.Tests` (persist/round-trip/corrupt-fallback cases for the new field; unknown values read back as System), `src/Wordwright.App/App.xaml.cs` (apply on startup and on change).

**UI.** A `CardControl` row in the "Wordwright" group, above "Start Wordwright when I sign in": label left, a `ui:ComboBox` right with three items (System / Light / Dark), bound two-way to the VM's `Theme`.

**Behaviour.** On change: save, then apply. In `App.xaml.cs` the existing `OnApplicationThemeChanged` accent-swap is reused verbatim; the only new logic is choosing the theme source:
- `System` → `SystemThemeWatcher.Watch(...)` as today (re-watch on switch back to System),
- `Light`/`Dark` → `SystemThemeWatcher.UnWatch(_mainWindow)` then `ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccents: false)` and run the accent swap manually.

Tray icon and tray menu (P11.3) keep following the **taskbar** theme, not the app theme — the tray lives on the taskbar.

**Acceptance:** unit tests green; *human check* — each of the three choices re-skins the window immediately (Mica included), survives a restart, and System tracks an OS theme change without a restart.

**Risks:** WPF-UI theme switching mid-session occasionally leaves stale control brushes on already-created pages — if a capture shows stale chrome, force a re-navigation after applying (navigate to the current page again); keep that workaround local to the theme setter.

---

## P11.3 Tray menu theming

**Why:** the tray menu is the most-seen surface (the window opens rarely) and is the last piece of stock chrome.

**Files:** new `src/Wordwright.App/Tray/TrayMenu.xaml` (resource dictionary with the menu styles), `src/Wordwright.App/Tray/TrayIconController.cs` (assign the styled menu; re-apply brushes when the taskbar theme changes — the same `WM_SETTINGCHANGE` "ImmersiveColorSet" path that already re-renders the tray icon).

**Design (palette-only, D10):** no new named colours. Derive the three surfaces the menu needs from existing palette colours with alpha overlays:
- Dark taskbar: background Anvil `#1A2030`, item hover = Steel `#E9ECF3` at ~8 % alpha, text Steel at full opacity (≈13:1 on Anvil), separator = Steel at 12 %.
- Light taskbar: background = white over Steel (renders as the pale blue-grey the palette already implies), hover = Forge ink at 8 %, text Anvil.

If the overlay maths produces a tint that looks wrong in the human check, add **one** named tint to DESIGN.md's palette table — that is a docs change, made openly, not a hardcoded hex slipped into XAML.

**Shape:** menu outer radius 8 (it is a window unto itself, so it takes the window tier), items 4 px, 4 px menu padding, item height ~28 px, checkmark for "Snippets on" drawn with the existing Phosphor-stroke style. Implement as implicit `Style`s for `ContextMenu` and `MenuItem` in `TrayMenu.xaml` (full `ControlTemplate` for `MenuItem` — stock template ignores most setters), then merge the dictionary in code when building the menu so it never touches the app window's resources.

**Acceptance:** *human check* — menu matches dark and light taskbars, hover/press/checkmark states read correctly, keyboard navigation (arrow keys, Enter, Esc) works, the menu still dismisses on click-away.

**Risks:** `ContextMenu` rendered from a tray icon has no owner window — make styles self-contained (no `DynamicResource` to app-theme brushes; resolve the concrete palette values at apply-time from code, where the taskbar theme is known). ~120 lines of template XAML is normal for this; keep it in the one file.

---

## P11.4 Motion.cs — the one motion helper

**File:** new `src/Wordwright.App/Motion.cs`. No XAML changes in this task; nothing uses it yet.

```csharp
// docs/DESIGN.md → "Motion (shipping app)": one system, one pattern.
public static class Motion
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    // Entrances: fade + 8 px rise, 200 ms, decelerate cubic-bezier(0,0,0,1) = CubicEase EaseOut.
    public static void Enter(FrameworkElement element, int delayMs = 0) { … }

    // Exits: fade out, 120 ms, accelerate (CubicEase EaseIn).
    public static void Exit(UIElement element, Action? completed = null) { … }

    // The D7 signature: glide a proxy element from one on-screen rect to another.
    public static void Glide(UIElement proxy, Rect from, Rect to, Action completed) { … }

    // Staggered entrance for a small set of elements (editor settle, 30 ms steps).
    public static void Settle(params FrameworkElement[] elements) { … }
}
```

Rules inside the helper: every method returns immediately when `!Enabled`; every animation sets `FillBehavior = Stop` (or removes itself on completion) so nothing holds a property forever; no repeating animations anywhere.

**Acceptance:** build green; the helper is provably inert with Windows animations off (unit-testing WPF animation in Core is out of scope — this is App code; verify by toggling the OS setting during P11.5's human check).

---

## P11.5 The signature move — connected animation, list → editor

**Prerequisite:** P11.4.

**Files:** `src/Wordwright.App/Pages/SnippetsPage.xaml` + `.xaml.cs`, `src/Wordwright.App/MainWindow.xaml.cs` (page entrances), `src/Wordwright.App/Resources/Theme.xaml` (chip style), possibly `src/Wordwright.App/Controls/OverlayCanvas.cs` (a small adorner-layer host if the page root can't host a floating proxy cleanly).

**1. Give the list a physical object to move.** Restyle the list item so the shortcut is a **chip**: 4 px radius, Steel background at low alpha, 12 px tabular text (the same `AppText` face, not monospace), name stays the primary line beneath. This makes the list scannable *and* gives the animation something real to carry into the editor.

**2. The glide (D7).** On `SelectionChanged` (user-initiated only — suppress on programmatic selection at window open and after add/delete):
1. `ContainerFromItem` the newly selected row; measure the chip's rect via `TransformToVisual(pageRoot)`.
2. Measure the editor's Shortcut field rect (the prefix+box row).
3. Stamp a proxy `Border` (chip style, same text) onto the overlay at the source rect.
4. `Motion.Glide` it to the target rect — position and width/height interpolated, 200 ms decelerate — then fade the proxy out in 60 ms while the real Shortcut field fades from 50 % to 100 % behind it.
5. `Motion.Settle` the Name / Text / Insert groups at 30 ms offsets.

Feels like Fluent's connected animation; no other expander does anything like it.

**3. The quiet system around it.** Page entrance: each page calls `Motion.Enter(root)` from `Loaded`. "Saved": replace the visibility toggle with fade-in then fade-out after 1.5 s (the existing `DispatcherTimer` keeps the timing; only the transition changes). List add: `Motion.Enter` on the new container via its `Loaded` event (only when the window is already open — not on first population). List remove: `Motion.Exit` on the row, removal deferred to the completion callback.

**Acceptance:** *human check*, twice: with "Show animations in Windows" **on** — select, navigate, save, add, delete all move once and stop; **off** — behaviour is pixel-identical to today, zero animations run. `check-screens.ps1` still reports zero unnamed controls (the proxy needs no automation name; it is decorative, `AutomationProperties.Visibility="Collapsed"`).

**Risks:** (a) `ContainerFromItem` returns null during virtualized scrolling — if the row is off-screen, skip the glide (the Settle still runs); (b) animation during rapid selection changes — cancel in-flight storyboards on the proxy before starting a new one; (c) keep the proxy off the visual tree of the list itself so scrollbars never measure it.

---

## P11.6 Playground completion moment

**Prerequisite:** P11.4. Copy exists: `Welcome.TryHere.Done` / the empty-state equivalent; the "after first expansion" flag already exists (P3.4c).

**Files:** `src/Wordwright.App/WelcomeWindow.xaml(.cs)`, `src/Wordwright.App/Pages/SnippetsPage.xaml(.cs)`, `src/Wordwright.App/Resources/Icons.xaml`.

**Change:** transcribe Phosphor's `check-circle` (Regular weight) into `Icons.xaml` as `IconCheckCircle`, matching the existing hand-transcribed style. On the first expansion inside a try-here box, reveal the done line with `Motion.Enter` and put the check-circle (16 px, `BrandAccentBrush`) to its left. Nothing else changes — the reveal that today snaps in will now arrive like everything else.

**Acceptance:** covered by P11.5's human check (with animations off the line simply appears, as today).

---

## P11.7 Housekeeping

**Files:** `scripts/check-screens.ps1`, `scripts/.gitignore`, git index.

1. Nav loop: `@("Snippets","AI actions","Offline AI","Settings","About")` → `@("Snippets","Settings","About")`.
2. `scripts/.gitignore`: add `ui-check-*.png` (the script comment already claims this is ignored — make it true).
3. `git rm --cached scripts/ui-check-*.png` and commit the removals; the files stay on disk for local use.

**Acceptance:** a fresh capture run prints no "nav item not found" lines; `git status` shows the PNGs untracked-ignored.

---

## After the craft pass

| Task | Owner | Note |
|---|---|---|
| P10.0 hero illustration | maintainer (by hand, Vistaprint → Inkscape per `brand/HERO_BRIEF.md`) | fills the About slot; also README + installer |
| P10.1 README + GIF | agent OK | do **last**, so the expansion GIF shows the finished UI and motion |
| P10.2 verification | human check on the real machine | now covers the craft pass: light/dark/high-contrast, 100/150/200 %, 800×600, keyboard-only, zero unnamed controls, animations on **and** off |
| P10.3 / P10.4 release | as planned | |

## Explicit non-goals for P11

- No new pages, nav items or copy. No "stats", expansion counters, tray badges or sounds (the playbook's repeating-animation/noise trap).
- No EleCho.WpfSuite or any other dependency: the connected animation is one overlay canvas and three storyboards — a library would be heavier than the problem. (The craft-studio library now lists it as the closest free option if P11.5's hand-roll ever proves unsound.)
- Ember stays unused; the parked AI screens are not touched; `Wordwright.Core` stays free of UI references.
