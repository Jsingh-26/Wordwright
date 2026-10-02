# Wordwright v1.0.0 — release notes (draft for P10.3)

Paste everything below the line into the GitHub release body. Title: **v1.0.0 (first stable release)**. Assets: everything in `Releases/v1.0.0/`, which is exactly `Wordwright-win-Setup.exe`, `Wordwright-win-Portable.zip`, `Wordwright-1.0.0-full.nupkg`, `RELEASES`, `releases.win.json` and `assets.win.json` (build with `scripts/pack-release.ps1 -Version 1.0.0`; there is no delta package). Fill in the verification numbers from the final run before publishing.

---

**Wordwright 1.0 — a private, offline text expander for Windows, by Unbound Kite.**

Type a short shortcut like `;sig` or `;date`, and Wordwright replaces it with your saved text, in any app: Word, Outlook, your browser, chat, the Windows search box.

### Install

Download **`Wordwright-win-Setup.exe`** below and run it. It installs for your account only; no administrator rights needed. Prefer not to install? **`Wordwright-win-Portable.zip`** runs from any folder.

Windows may show *"Windows protected your PC"* the first time, because the installer is not signed with a paid certificate. Click **More info → Run anyway**. The [README](https://github.com/Jsingh-26/Wordwright#windows-protected-your-pc) explains why.

### What Wordwright does

- **Snippets.** Type `;` and a shortcut, and your text appears. No limit on how many you keep or how long they are.
- **Variables.** `{date}`, `{time}`, `{clipboard}` and `{cursor}` fill themselves in as a snippet expands.
- **A focused editor.** A searchable list, an editor that saves as you type, export and import as JSON, and a "try it here" box on first run.
- **Stays out of the way.** It lives in the tray, can start when you sign in, and can be switched off in chosen apps such as your password manager.
- **Light, dark or system theme**, and animations follow the Windows "Show animations" setting.

### Private by design

- **No network access at all.** No telemetry, no analytics, no crash reports, no update check.
- **Nothing you type is stored.** Only the last 64 characters are held in memory to spot a shortcut, and they are cleared constantly. Nothing you type, copy or save as a snippet is written to a log.
- **Your clipboard comes back** straight after a paste, and the paste stays out of clipboard history (Win+V).
- **Your data lives in one place:** `%AppData%\Wordwright`.

### What changed since v0.2.0

- **A finished interface:** a connected animation from the snippet list to the editor, page transitions, a Fluent-styled tray menu, a Theme setting, and clearer error colours.
- **Works at small window sizes:** every page now scrolls, so all settings are reachable at 800×600.
- **Half the download:** the installer drops from 160 MB to 78 MB. Leftover native libraries from the parked AI feature were still being packaged; they are gone.
- **Brand:** a new illustration on the About page and the installer, the app's mark on the window and taskbar, and the publisher name, Unbound Kite.
- **About page** now says exactly what the app does on the network: nothing.
- **Works with your keyboard layout:** shortcuts expand on German, French, Spanish and UK layouts, with Caps Lock on, with AltGr characters, and with a prefix that needs Shift, such as `:`.
- **Sharp on every screen:** text and icons stay crisp when monitors use different scaling, and the window reopens on the monitor where you closed it and fits small screens.
- **Safer pasting:** your clipboard is put back without slowing the paste down, even after copying a large Excel range, and slow apps get time to read the snippet first. If another program is holding the clipboard, the shortcut is left as you typed it rather than pasting the wrong thing.
- **Multi-line snippets** keep their line breaks in every app.
- **Keeps working after sleep and lock:** the shortcut listener comes back on its own after the PC resumes or unlocks, and the tray says so if Windows refuses it.
- **Edits are never lost:** the editor saves when you switch snippets, change page or quit, and a shortcut prefix that cannot work is not applied.
- **One data folder:** the Microsoft Store and installer versions both keep your snippets in `%AppData%\Wordwright`, and the portable zip no longer adds itself to Start with Windows.
- **A small, content-free event log** in `%AppData%\Wordwright\logs` records only events such as "started", never what you type, so a problem can be reported without sharing your text.

### Verification

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 181 passed, 0 failed.
- Every page captured at 800×600 and maximised, in light and dark: zero controls without an accessible name.
- Manual pass on a real Windows machine: ___ (P10.2 and the release checklist in `docs/PLAN.md`).

### The AI writing assistant

Releases v0.1.3–v0.1.5 also had an on-device AI rewriter. It is parked, not deleted: [`docs/AI_REWRITING.md`](https://github.com/Jsingh-26/Wordwright/blob/main/docs/AI_REWRITING.md) tells the whole story, and the code is at the [`ai-rewriting`](https://github.com/Jsingh-26/Wordwright/tree/ai-rewriting) tag.

Wordwright is free and open source (MIT).
