# UX copy

Every user-facing string. Implement as `Wordwright.App/Resources/Strings.resx` using these IDs. Voice: plain, direct, sentence case, active verbs, no apologies, no exclamation marks. An action keeps the same name everywhere ("Turn on offline AI" button → "Offline AI is on" confirmation).

Placeholders use `{Name}`.

## Tray
| ID | Text |
|---|---|
| Tray.Open | Open Wordwright |
| Tray.SnippetsOn | Snippets on |
| Tray.AiOn | Offline AI on |
| Tray.TurnOnAi | Turn on offline AI… |
| Tray.Quit | Quit Wordwright |
| Tray.Tooltip | Wordwright: type {Prefix} + a shortcut, or select text and press {PaletteHotkey} |
| Tray.TooltipBetterModel | Wordwright: a better AI model is available for your PC |

## First run (welcome, shown once)
| ID | Text |
|---|---|
| Welcome.Title | Wordwright is running |
| Welcome.Body | Type {Prefix}date anywhere to try it. Your snippets live in the tray icon. |
| Welcome.AiHint | Want AI rewriting too? It runs on this PC, with no account and no cloud. You can turn it on any time. |
| Welcome.Primary | Show my snippets |
| Welcome.Secondary | Turn on offline AI |
| Welcome.TryHere | Try it: type {Prefix}date here |
| Welcome.TryHere.Done | That works everywhere: Word, Outlook, your browser, any app. |

## Navigation
| ID | Text |
|---|---|
| Nav.Snippets | Snippets |
| Nav.Actions | AI actions |
| Nav.OfflineAi | Offline AI |
| Nav.Settings | Settings |
| Nav.About | About |

## Snippets page
| ID | Text |
|---|---|
| Snippets.Title | Snippets |
| Snippets.New | New snippet |
| Snippets.Search | Search snippets |
| Snippets.Empty | No snippets yet. Create one, then type {Prefix} and its shortcut in any app. |
| Snippets.Empty.TryHere | Or try an example first: type {Prefix}date here |
| Snippets.Field.Name | Name |
| Snippets.Field.Shortcut | Shortcut |
| Snippets.Field.ShortcutHelp | Type {Prefix}{Shortcut} in any app to insert this text. |
| Snippets.Field.Text | Text |
| Snippets.Insert | Insert |
| Snippets.Insert.Date | Date |
| Snippets.Insert.Time | Time |
| Snippets.Insert.Clipboard | Clipboard |
| Snippets.Insert.Cursor | Cursor position |
| Snippets.Preview | Inserts as: |
| Snippets.Preview.Clipboard | (what you copied last) |
| Snippets.Saved | Saved |
| Snippets.Delete | Delete snippet |
| Snippets.DeleteConfirm | Delete "{Name}"? You can't undo this. |
| Snippets.Error.ShortcutTaken | {Prefix}{Shortcut} is already used by "{Name}". |
| Snippets.Error.ShortcutInvalid | Use letters, numbers, - or _ only, up to 32 characters. |
| Snippets.Warn.VeryLong | This snippet is very long. It will still work, but may take a moment to insert. |

## AI actions page
| ID | Text |
|---|---|
| Actions.Title | AI actions |
| Actions.New | New action |
| Actions.Field.Name | Name |
| Actions.Field.Letter | Letter in the palette |
| Actions.Field.Hotkey | Hotkey |
| Actions.Field.HotkeyHelp | Select text anywhere and press this to run the action straight away. |
| Actions.Hotkey.Record | Press the keys you want to use |
| Actions.Hotkey.None | No hotkey |
| Actions.Hotkey.Clear | Remove hotkey |
| Actions.Hotkey.InUse | {Hotkey} is used by another app. Choose a different one. |
| Actions.Hotkey.Duplicate | {Hotkey} is already set for "{Action}". |
| Actions.Hotkey.Invalid | Use Ctrl, Alt, Shift or Win together with a letter, number or function key. |
| Actions.Hotkey.Reserved | Windows keeps {Hotkey} for itself. Choose a different one. |
| Actions.Field.Instruction | Instruction |
| Actions.Field.InstructionHelp | Tell the AI what to do with the selected text, in one or two sentences. |
| Actions.TryIt | Try it |
| Actions.TryItPlaceholder | Paste some text here to test this action |
| Actions.ResetBuiltIn | Reset to default |
| Actions.NeedsAi | Turn on offline AI to try actions here. |

### Built-in actions
| id | Name | Letter | Default hotkey | Instruction |
|---|---|---|---|---|
| fix | Fix grammar and spelling | G | Ctrl+Alt+G | Correct grammar, spelling and punctuation. Keep the meaning, tone and language. Change as little as possible. |
| clear | Make it clearer | C | none | Rewrite so it is clear and easy to read. Keep the meaning and roughly the same length. |
| formal | More formal | F | none | Rewrite in a polite, professional tone suitable for work email. Keep the meaning. |
| friendly | More friendly | R | none | Rewrite in a warm, friendly, natural tone. Keep the meaning. |
| shorten | Shorten | S | none | Make it shorter and more direct. Keep every important point. |
| custom | Custom instruction… | I | none | (user types the instruction in the palette) |

## Action palette and pill
| ID | Text |
|---|---|
| Palette.Filter | Type to filter… |
| Palette.Snippets | Snippets |
| Palette.Snippets.Empty | No snippets match. |
| Palette.CustomPlaceholder | What should I do with the selected text? |
| Palette.AiOff.Title | Offline AI is off |
| Palette.AiOff.Body | Turn it on to rewrite selected text on this PC. |
| Palette.AiOff.Button | Turn on offline AI |
| Pill.Loading | Loading the AI model… |
| Pill.Working | Rewriting… {Seconds} s |
| Pill.Cancel | Esc to cancel |
| Pill.Done | Done. Ctrl+Z undoes it. |
| Pill.Ruler.Name | Progress against the expected time (screen-reader name of the ruler tick) |
| Pill.Cancelled | Cancelled. Your text is unchanged. |
| Pill.NoSelection | Select some text first. |
| Pill.TooLong | That's too much text for one rewrite. Select up to about 1,000 words. |
| Pill.BadOutput | The AI's answer didn't look right, so your text is unchanged. Try again or pick another action. |
| Pill.AdminApp | Wordwright can't type into apps running as administrator. Run Wordwright as administrator too, or use another app. |
| Pill.AdminApp.Copied | Copied the rewrite. Wordwright can't type into apps running as administrator, so paste it yourself. |
| Pill.HotkeyTaken | {Hotkey} is used by another app. Choose a different one in Wordwright. |
| Pill.AiOff | Offline AI is off. Open Wordwright to turn it on. |
| Pill.NotEnoughResources | Offline AI needs more memory or disk space than this PC has free right now. Your text is unchanged. |

## Turn on offline AI
| ID | Text |
|---|---|
| Ai.Off.Title | Offline AI |
| Ai.Off.Body | Rewrite selected text with an AI model that runs on this PC. There's no account, no API key and no cloud. Your text never leaves this computer. Wordwright downloads the model once, only after you agree. |
| Ai.Off.Button | Turn on offline AI |
| Ai.Checking | Checking what this PC can run… |
| Ai.Rec.Title | Recommended for your PC |
| Ai.Rec.ModelLine | {ModelName} |
| Ai.Rec.MetaLine | {Size} download, {License} licence |
| Ai.Rec.YourPc | Your PC: {Ram} memory, {Cpu}, {GpuSummary} |
| Ai.Rec.GpuNone | no graphics card suitable for AI |
| Ai.Rec.GpuYes | {GpuName} with {Vram} for AI |
| Ai.Rec.OneLine | One line |
| Ai.Rec.Paragraph | Short paragraph |
| Ai.Rec.Range | {Min}–{Max} seconds |
| Ai.Rec.RangeOver | {Max}+ seconds |
| Ai.Rec.NoRam | There isn't enough free memory to run a model right now. Wordwright checks again the next time it starts. |
| Ai.Unavailable.Title | Offline AI isn't available on this PC right now |
| Ai.Unavailable.Ram | There isn't enough free memory to run a model. Wordwright checks again each time it starts, so this can change by itself. Snippets still work. |
| Ai.Unavailable.Disk | There isn't enough free disk space to download a model. Wordwright checks again each time it starts. Snippets still work. |
| Ai.Unavailable.None | Wordwright has no model for a PC this size. Snippets still work. |
| Ai.Unavailable.Import | Offline AI isn't available on this PC right now, so Wordwright didn't add that model. |
| Ai.Rec.None | There's no model ready to offer yet. Wordwright will check again later. |
| Ai.Rec.GoodAt | Good at: {Strengths} |
| Ai.Rec.NotSoGood | Not so good at: {Weaknesses} |
| Ai.Rec.LoadNote | The first rewrite after starting your PC takes about {LoadSeconds} seconds longer while the model loads. |
| Ai.Rec.EstimateNote | These are estimates. Wordwright measures the real speed after downloading. |
| Ai.Rec.Privacy | After this one download, rewriting works with no internet connection. |
| Ai.Rec.Other | Show other options |
| Ai.Rec.OtherSlow | May be slow on this PC |
| Ai.Rec.StepDownRam | We picked a smaller model because only {FreeRam} of memory is free right now. |
| Ai.Rec.Minimal | This PC can run only a very small model. It fixes basic grammar and may be slow. You can skip AI and keep using snippets. |
| Ai.Rec.NoDisk | You need {Needed} of free space on {Drive}. Free up space and try again. |
| Ai.Rec.Primary | Download and turn on |
| Ai.Rec.Secondary | Not now |
| Ai.Rec.Import | Import model file instead |
| Ai.Import.NotModel | That file isn't a GGUF model. Pick the .gguf file you downloaded. |
| Ai.Import.Failed | Wordwright couldn't read that file. Copy it somewhere else and import it again. |
| Ai.Dl.Title | Downloading {ModelName} |
| Ai.Dl.Progress | {Done} of {Total}, {Speed}, about {TimeLeft} left |
| Ai.Dl.Cancel | Cancel download |
| Ai.Dl.Resume | Resume download |
| Ai.Dl.Failed | The download stopped. Check your internet connection, then resume. |
| Ai.Verify | Checking the file… |
| Ai.Verify.Failed | The downloaded file doesn't match what we expected, so Wordwright deleted it. Try downloading again. |
| Ai.Calibrate | Measuring speed on your PC… |
| Ai.Done.Title | Offline AI is on |
| Ai.Done.Body | Measured on your PC: one line about {LineSeconds} s, short paragraph about {ParaSeconds} s. Select text anywhere and press {FixHotkey} to fix grammar, or {PaletteHotkey} for all actions. |
| Ai.Done.Button | Done |

## Offline AI page (when on)
| ID | Text |
|---|---|
| Ai.On.ActiveModel | Active model |
| Ai.On.Measured | Measured on this PC: one line {LineSeconds} s, short paragraph {ParaSeconds} s |
| Ai.On.CheckBetter | Check for a better model |
| Ai.On.Change | Change model |
| Ai.On.Import | Import model file |
| Ai.On.WeeklyToggle | Check weekly for better models |
| Ai.On.WeeklyHelp | Wordwright downloads a small list of models once a week. None of your text is sent. |
| Ai.On.Unload | Free up memory after {Minutes} minutes without use |
| Ai.On.TurnOff | Turn off offline AI |
| Ai.On.RemoveModel | Remove model file ({Size}) |
| Ai.Better.Banner | A better model for your PC: {ModelName}. {Improvement}. {Size} download. |
| Ai.Better.ImprovementQuality | Rewrites score noticeably higher in our tests |
| Ai.Better.ImprovementSpeed | About {Percent}% faster on PCs like yours |
| Ai.Better.Details | Details |
| Ai.Better.Download | Download |
| Ai.Better.NotNow | Not now |
| Ai.Better.NoneFound | You already have the best model for this PC. |
| Ai.Better.Offline | Couldn't reach the model list. Check your internet connection and try again. |
| Ai.Switch.KeepOld | Your current model stays active until the new one is ready. |
| Ai.Switch.DeleteOld | Remove the previous model ({Size}) to free up space? |
| Ai.Switch.Back | Switch back to {ModelName} |
| Ai.Import.Unverified | Custom model (not verified by Wordwright). Speed and quality are unknown until it's measured. |
| Ai.Import.NotGguf | That file isn't a GGUF model. Choose a file ending in .gguf. |

## Settings
Group headings sit above their rows (docs/DESIGN.md §8).
| ID | Text |
|---|---|
| Settings.Title | Settings |
| Settings.Group.Snippets | Snippets |
| Settings.Group.Ai | AI |
| Settings.Group.Wordwright | Wordwright |
| Settings.PaletteHotkey | Hotkey for all AI actions |
| Settings.AiTurnOff | Offline AI is on |
| Settings.AiTurnOffHelp | This PC hasn't the memory or disk a model needs, so Wordwright won't load it. Turn it off here if you don't want it any more. |
| Settings.AiTurnOffButton | Turn off |
| Settings.ActionHotkeysHelp | Give single actions their own hotkeys on the AI actions page. |
| Settings.Prefix | Snippet prefix |
| Settings.StartWithWindows | Start Wordwright when I sign in |
| Settings.Theme | Theme |
| Settings.ExcludedApps | Turn Wordwright off in these apps |
| Settings.ExcludedAppsHelp | Wordwright can't always tell when you're typing a password. List apps where it should stay quiet, such as password managers. |
| Settings.AppUpdates | Check weekly for Wordwright updates |
| Settings.OpenDataFolder | Open data folder |
| Settings.ExcludedApps.Add | Add app |
| Settings.ExcludedApps.Remove | Remove |
| Settings.Export | Export snippets |
| Settings.Import | Import snippets |
| Settings.ImportDone | Added {Count} snippets. |
| Settings.ImportSkipped | Skipped {Count} whose shortcut is already in use. |
| Settings.ImportFailed | That file isn't a snippets export. |

## About
| ID | Text |
|---|---|
| About.Version | Version {Version} |
| About.Body | Wordwright is free and open source (MIT). Snippets and AI rewriting run entirely on this PC. |
| About.Privacy | Wordwright has no telemetry. It uses the internet only to download a model you chose, or to check for updates if you turned that on. |
| About.Source | View source on GitHub |
| About.ThirdParty | Third-party licences |
| About.Licence.Zodiak | Zodiak — ITF Free Font Licence |
| About.Licence.Phosphor | Phosphor — MIT |
| About.Licence.WpfUi | WPF-UI — MIT |
| About.Licence.NotifyIcon | H.NotifyIcon — MIT |
| About.Licence.LlamaSharp | LLamaSharp — MIT |
| About.Licence.Velopack | Velopack — MIT |

## Shared
| ID | Text |
|---|---|
| Dialog.Cancel | Cancel |

## Debug page (debug builds only, docs/PLAN.md P4.1)
| ID | Text |
|---|---|
| Debug.Title | Hardware |
| Debug.Tier | Tier |
| Debug.Cpu | Processor |
| Debug.Cores | Physical cores |
| Debug.Memory | Memory |
| Debug.MemoryFree | Memory free now |
| Debug.Avx2 | AVX2 |
| Debug.Avx512 | AVX-512 |
| Debug.Gpus | Graphics |
| Debug.Disk | Free disk |
| Debug.OsBuild | Windows build |
| Debug.Yes | Yes |
| Debug.No | No |

## Accessibility (automation names for controls that have no visible label)
| ID | Text |
|---|---|
| A11y.Minimize | Minimise |
| A11y.Maximize | Maximise |
| A11y.Close | Close |
| A11y.NavToggle | Show or hide the menu |
| A11y.PageUp | Page up |
| A11y.PageDown | Page down |
