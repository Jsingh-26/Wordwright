# UX copy

Every user-facing string. Implement as `Wordwright.App/Resources/Strings.resx` using these IDs. Voice: plain, direct, sentence case, active verbs, no apologies, no exclamation marks. An action keeps the same name everywhere ("Turn on offline AI" button → "Offline AI is on" confirmation).

Placeholders use `{Name}`.

## Tray
| ID | Text |
|---|---|
| Tray.Open | Open Wordwright |
| Tray.SnippetsOn | Snippets on |
| Tray.Quit | Quit Wordwright |
| Tray.Tooltip | Wordwright: type {Prefix} + a shortcut to insert a snippet |

## First run (welcome, shown once)
| ID | Text |
|---|---|
| Welcome.Title | Wordwright is running |
| Welcome.Body | Type {Prefix}date anywhere to try it. Your snippets live in the tray icon. |
| Welcome.Primary | Show my snippets |
| Welcome.TryHere | Try it: type {Prefix}date here |
| Welcome.TryHere.Done | That works everywhere: Word, Outlook, your browser, any app. |

## Navigation
| ID | Text |
|---|---|
| Nav.Snippets | Snippets |
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

## Settings
Group headings sit above their rows (docs/DESIGN.md §8).
| ID | Text |
|---|---|
| Settings.Title | Settings |
| Settings.Group.Snippets | Snippets |
| Settings.Group.Wordwright | Wordwright |
| Settings.Prefix | Snippet prefix |
| Settings.Error.PrefixInvalid | Use 1 to 3 symbols, such as ; or //. |
| Settings.StartWithWindows | Start Wordwright when I sign in |
| Settings.Theme | Theme |
| Settings.Theme.System | System |
| Settings.Theme.Light | Light |
| Settings.Theme.Dark | Dark |
| Settings.ExcludedApps | Turn Wordwright off in these apps |
| Settings.ExcludedAppsHelp | Wordwright can't always tell when you're typing a password. List apps where it should stay quiet, such as password managers. |
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
| About.Publisher | by Unbound Kite |
| About.Version | Version {Version} |
| About.Body | Wordwright is free and open source (MIT). Your snippets stay on this PC. |
| About.Privacy | Wordwright has no telemetry and never connects to the internet. Nothing you type leaves this PC. |
| About.Source | View source on GitHub |
| About.Support | Support Wordwright |
| About.ThirdParty | Third-party licences |
| About.Licence.Zodiak | Zodiak — ITF Free Font Licence |
| About.Licence.Phosphor | Phosphor — MIT |
| About.Licence.WpfUi | WPF-UI — MIT |
| About.Licence.NotifyIcon | H.NotifyIcon — MIT |
| About.Licence.Velopack | Velopack — MIT |

## Shared
| ID | Text |
|---|---|
| Dialog.Cancel | Cancel |

## Accessibility (automation names for controls that have no visible label)
| ID | Text |
|---|---|
| A11y.Minimize | Minimise |
| A11y.Maximize | Maximise |
| A11y.Close | Close |
| A11y.NavToggle | Show or hide the menu |
| A11y.PageUp | Page up |
| A11y.PageDown | Page down |
