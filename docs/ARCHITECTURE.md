# Architecture

## Product in one paragraph

Wordwright lives in the Windows system tray. It watches for snippet triggers (e.g. `;sig`) as you type and replaces them with the stored text. When you select text and press an AI hotkey, a local language model rewrites the selection in place: either directly (each action can have its own hotkey, e.g. Ctrl+Alt+G for grammar) or through a small action palette (Ctrl+Alt+Space). The model runs on-device through llama.cpp. The app talks to the internet only to download a model (after consent) or to check for a better one (opt-in).

## Solution layout

```
Wordwright.sln
src/
  Wordwright.Core/          Pure .NET class library. No UI, no Win32. Unit-tested.
    Snippets/          Snippet model, store, trigger matcher, variable expander
    Actions/           AI action model, store, prompt builder, output cleaner
    Hardware/          HardwareProfile record, tier classifier
    Models/            Catalog (models.json) parser, recommender, speed estimator,
                       ModelDownloader, CatalogUpdater, installed-model registry
    Settings/          Settings model + JSON store
  Wordwright.Platform/      Windows-only glue: keyboard hook, hotkeys, clipboard,
                       SendInput, foreground window, hardware probing (WMI/DXGI)
  Wordwright.Inference/     LLamaSharp wrapper: load/unload, generate, calibrate
  Wordwright.App/           WPF app: tray icon, windows, dialogs, action palette,
                       progress pill, app lifetime, wiring
tests/
  Wordwright.Core.Tests/    xUnit tests for everything in Wordwright.Core
models/
  models.json          Curated model catalog (also embedded in the app at build time)
eval/                  Python evaluation harness (not shipped)
```

WPF-UI allows one `ContentDialogHost` per window and throws when a second one registers. It belongs on `MainWindow`, never on a page: WPF-UI builds a page again every time the user navigates back to it, so a host on a page takes the app down on the second visit.

## Packaging

Two builds ship from the same source. Both keep the user's data in the same place; they differ in how they are installed and how they start with Windows.

- **Installer (GitHub Releases):** `scripts/pack-release.ps1` publishes the app self-contained and packs it with Velopack. `App.xaml.cs` calls `VelopackApp.Build().Run()` in its constructor, the first thing the generated entry point does, so an install or uninstall can finish. Nothing ever checks for an update unless the user turns that on (P8).
- **Microsoft Store package:** `scripts/pack-msix.ps1` packs `packaging/AppxManifest.xml` with the tile assets `scripts/IconGen --msix` renders from the brand geometry. It is a full-trust (`runFullTrust`) desktop package; the manifest's Identity is a placeholder until Partner Center reserves the name.
- Both builds target `net10.0-windows10.0.19041.0`, which is what gives the app the WinRT `StartupTask` API; the manifest declares Windows 10 19045 as the floor.
- **Start with Windows** goes through `Wordwright.Platform.Startup.StartWithWindows`: the `HKCU\...\Run` key when unpackaged, the manifest's `desktop:startupTask` when packaged (Store policy forbids the Run key). `PackageIdentity.IsPackaged` decides which.

## Dependencies (the complete allowed list)

| Package | Used in | Why |
|---|---|---|
| `WPF-UI` (lepoco) | App | Windows 11 Fluent controls, Mica backdrop, light/dark theme |
| `H.NotifyIcon.Wpf` | App | Tray icon and tray menu |
| `CommunityToolkit.Mvvm` | App | MVVM boilerplate |
| `LLamaSharp` | Inference | .NET bindings for llama.cpp, loads GGUF models |
| `LLamaSharp.Backend.Cpu` | Inference | CPU inference (always shipped) |
| `LLamaSharp.Backend.Vulkan` | Inference | GPU inference on any vendor (used when a GPU is detected) |
| `Vortice.DXGI` | Platform | Read true dedicated GPU memory (WMI caps at 4 GB) |
| `System.Management` | Platform | WMI: CPU name, cores |
| `Velopack` | App | Installer and (opt-in) app updates from GitHub Releases |
| `xunit`, `FluentAssertions` | Tests | Unit tests |

Not packages but bundled assets: Zodiak font files (Fontshare, embedded as WPF resources) and Phosphor icons (MIT) copied as XAML path geometries into `Wordwright.App/Resources/Icons.xaml`. Icons and logo come from `brand/`.

Pin exact versions in `Directory.Packages.props` (central package management).

## Data files

User data lives under `%AppData%\Wordwright\` (small). Models live under `%LocalAppData%\Wordwright\models\` (large, not roamed).

### `snippets.json`
```json
{
  "schemaVersion": 1,
  "triggerPrefix": ";",
  "snippets": [
    { "id": "b1c2", "trigger": "sig", "name": "Email signature",
      "body": "Best regards,\nJaspreet", "enabled": true,
      "createdUtc": "2026-10-01T10:00:00Z", "updatedUtc": "2026-10-01T10:00:00Z" }
  ]
}
```
- `trigger` is stored without the prefix; unique, case-sensitive, 1–32 chars, letters/digits/`-`/`_` only.
- `body` has no length limit (the editor warns above 100,000 characters, for performance only).
- Writes are atomic: write to `snippets.json.tmp`, then replace. Keep `snippets.json.bak` of the previous version.

### `actions.json`
```json
{
  "schemaVersion": 1,
  "actions": [
    { "id": "fix", "name": "Fix grammar and spelling", "shortcutKey": "G",
      "instruction": "Correct grammar, spelling and punctuation. Keep the meaning, tone and language. Change as little as possible.",
      "hotkey": "Ctrl+Alt+G", "builtIn": true, "enabled": true }
  ]
}
```
`hotkey` is optional (`null` = palette only). By default only `fix` has one (Ctrl+Alt+G); more defaults would risk clashing with other apps. Built-in actions and their instructions are listed in `UX_COPY.md`. Users may add, edit, reorder and disable actions; built-ins can be reset to default.

### `settings.json`
```json
{
  "schemaVersion": 1,
  "paletteHotkey": "Ctrl+Alt+Space",
  "snippetsEnabled": true,
  "startWithWindows": true,
  "aiEnabled": false,
  "activeModelId": null,
  "unloadAfterIdleMinutes": 10,
  "checkForBetterModelsWeekly": false,
  "checkForAppUpdatesWeekly": false,
  "lastCatalogCheckUtc": null,
  "excludedApps": ["KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe"],
  "theme": "system"
}
```

### `installed-models.json` (in `%LocalAppData%\Wordwright\`)
One record per downloaded or imported model: `id`, `file`, `sha256`, `verified` (true for catalog downloads, false for manual imports), `calibration` (prompt tokens/sec, generation tokens/sec, load seconds, backend, measured date).

## Snippet engine

1. `Wordwright.Platform.KeyboardHook` installs `SetWindowsHookEx(WH_KEYBOARD_LL)` on a dedicated thread with its own message loop. The callback must return in well under 1 ms: enqueue and return; never block.
2. Ignore events with the `LLKHF_INJECTED` flag (our own `SendInput` output) to avoid loops.
3. Translate printable keys with `ToUnicodeEx` using the foreground window's keyboard layout and append to a rolling in-memory buffer (max 64 chars). Backspace removes one char. Enter, Esc, Tab, arrows, Home/End, PgUp/PgDn, mouse clicks and foreground-window changes clear the buffer, and so does a pause of more than 5 s between two typed characters (decision D3), so a trigger typed with a long gap in the middle of it never expands. Nothing happens while the foreground process is in `excludedApps`.
4. After each character, `Wordwright.Core.Snippets.TriggerMatcher` checks whether the buffer ends with `prefix + trigger`, on a word boundary (the char before the prefix is start-of-buffer, whitespace or punctuation). If one trigger is a prefix of another (`;s` and `;sig`), expand only when the next typed char is a space or punctuation (then the space/punctuation is kept after the expansion). Otherwise expand immediately.
5. On a match: send Backspaces for the typed trigger (prefix + trigger length), then paste the expanded body.
6. Variables expanded by `VariableExpander` before paste: `{date}` (system short date), `{time}` (short time), `{clipboard}` (current clipboard text), `{cursor}` (caret ends here: after paste, send Left-arrow presses for the characters after the marker). Unknown `{...}` stay as typed. `{{` produces a literal `{`.

## Paste and selection capture (shared)

- **Paste:** save clipboard (text plus best-effort other formats) → set clipboard to our text, also adding the `ExcludeClipboardContentFromMonitorProcessing` format so it stays out of Windows clipboard history (Win+V) → `SendInput` Ctrl+V → wait 150 ms → restore the saved clipboard.
- **Capture selection:** save clipboard → clear → `SendInput` Ctrl+C → poll for new text up to 400 ms → read → restore clipboard. Empty result means "nothing selected".
- **Known limitation:** Windows blocks input from a normal app into apps running as administrator (UIPI). Detect an elevated foreground window and show the message from `UX_COPY.md` instead of failing silently.

## AI rewrite flow

### Hotkeys
- `Wordwright.Platform.HotkeyService` registers, with `RegisterHotKey` on a hidden message window: the palette hotkey (default Ctrl+Alt+Space) and every enabled action's own hotkey.
- Registration happens at start-up and whenever settings or actions change (unregister all, register again).
- If a combination fails to register (another app owns it), keep the setting, mark it "In use by another app" in the UI, and show the pill message once when the user presses it. Never silently steal or drop it.
- Validation: at least one of Ctrl/Alt/Shift/Win plus one key; reject combinations Windows reserves (e.g. Win+L, Ctrl+Alt+Del) and duplicates within Wordwright.
- The hotkey recorder in the UI captures the next key combination while focused, and Esc cancels recording.

### Flow
1. User selects text and presses either an action's own hotkey (go to step 5 with that action) or the palette hotkey.
2. If AI is off, show the "Turn on offline AI" invitation (palette) or the AI-off pill message (direct hotkey) instead of acting.
3. Capture the selection. If empty, show the "Select some text first" pill.
4. Palette only: it appears near the caret (fallback: near the mouse). Type to filter, arrows + Enter, or the action's letter. Esc closes. "Custom instruction" opens a one-line input.
5. The progress pill appears near the caret with elapsed seconds; Esc cancels generation.
6. `PromptBuilder` builds the messages below. `Wordwright.Inference` generates using the chat template embedded in the GGUF.
7. `OutputCleaner` post-processes; if it rejects the output, show the error copy and paste nothing.
8. Paste over the still-selected text. The pill shows the "done" copy for 3 s. The target app's own Ctrl+Z undoes the change.

### Prompt
System message:
```
You rewrite text. Follow the instruction exactly.
Reply with only the rewritten text: no introduction, no quotes, no notes, no explanation.
Keep the original language unless the instruction says otherwise.
Keep names, numbers, links and formatting such as line breaks and bullet points.
```
User message:
```
Instruction: {action.instruction}

Text:
{selected text}
```

### Generation parameters
- Context 4,096 tokens. If input exceeds ~1,500 tokens, show the "too long" copy (v1 limit).
- Temperature 0.3, top-p 0.9, repeat penalty 1.05.
- Max new tokens = min(2 × input tokens + 64, 1,536).
- Disable "thinking" for models that have it, per the catalog entry's `promptHints`.

### OutputCleaner rules (all unit-tested)
1. Remove `<think>…</think>` blocks.
2. Remove leading preamble lines such as `^(sure|okay|of course|certainly|here('s| is| are)|rewritten|corrected|revised)\b.*:?\s*$` (case-insensitive).
3. Remove one pair of wrapping quotes or a wrapping code fence if the input had none.
4. Remove trailing lines starting with `Note:`, `Explanation:`, `(Note`, `I changed`, `I made`, `I corrected`, `Changes:`.
5. Trim; keep the input's trailing newline if it had one.
6. Reject if empty, or if longer than 4 × input length + 200 characters.

## Model lifecycle
- **Load:** in the background on first AI use after start; the pill shows the "loading" copy.
- **Unload:** after `unloadAfterIdleMinutes` without AI use.
- **Backend:** Vulkan when a GPU with enough free dedicated memory for the whole model + 1 GB exists; otherwise CPU. No partial offload in v1.
- **Threads:** physical cores − 1 (minimum 2).

## Hardware probing (`Wordwright.Platform.HardwareProbe`)
Returns `HardwareProfile`: total and available RAM (`GlobalMemoryStatusEx`), CPU name and physical cores (WMI `Win32_Processor`), AVX2/AVX-512 (`System.Runtime.Intrinsics.X86`), GPUs with dedicated memory (DXGI; skip "Microsoft Basic Render Driver"), free disk on the `%LocalAppData%` drive, OS build.

## Catalog, recommendation, download
Schema, tier rules and estimate formula: `docs/MODELS.md`.
- The catalog is embedded at build time. It is refreshed from `https://raw.githubusercontent.com/Jsingh-26/Wordwright/main/models/models.json` only when the user clicks "Check for a better model" or has opted into the weekly check.
- `ModelDownloader`: `HttpClient` with HTTP Range resume into `<file>.part`, progress, cancel, SHA-256 verify, atomic rename. Check free disk ≥ size × 1.2 first.
- **Import model file:** user picks a `.gguf`; check GGUF magic bytes; if its SHA-256 matches a catalog entry, treat as that entry (verified); otherwise register as a custom, unverified model with no estimate until calibrated.
- **Switching models:** download → verify → calibrate → switch `activeModelId` → only then offer to delete the old file.

## Calibration
After a download or import: load the model, run the fixed calibration prompt (a 60-word paragraph with the "Fix grammar" action) twice, discard the first run, record prompt tokens/sec, generation tokens/sec and load time. Show real times for "one line" and "short paragraph" using the formula in `MODELS.md`.

## Privacy summary
- No telemetry. No content in logs. Log file `%LocalAppData%\Wordwright\logs\wordwright-YYYYMMDD.log`, events only, 7-day retention.
- The keystroke buffer is never persisted.
- Windows does not reliably tell other apps when a password field is focused, so `excludedApps` lets users turn Wordwright off in specific programs; the README says this plainly.
