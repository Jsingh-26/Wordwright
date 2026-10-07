# Microsoft Store submission kit (P10.4)

Everything Partner Center asks for, ready to paste. The package is `Releases/Wordwright-1.0.0.msixbundle`, built by `scripts/pack-msix.ps1 -Version 1.0.0` (unsigned; the Store signs it).

## Before the first upload (maintainer, once)

1. **Developer account:** [partner.microsoft.com](https://partner.microsoft.com/dashboard/registration) → individual account. Publisher display name: **Unbound Kite**.
2. **Reserve the app name:** Apps and games → New product → MSIX or PWA app → **Wordwright**.
3. **Product identity:** Product management → Product identity gives three values. Put them into `packaging/AppxManifest.xml` (or hand them to the agent):
   - `Package/Identity/Name` → `Identity Name` (replaces `Jsingh26.Wordwright`)
   - `Package/Identity/Publisher` → `Identity Publisher` (replaces `CN=Jsingh26`)
   - `Package/Properties/PublisherDisplayName` → already **Unbound Kite**; must match the account exactly
   Then rebuild with `scripts/pack-msix.ps1 -Version 1.0.0`. The upload is refused if any of the three differ.

## Pricing and availability

| Field | Value |
|---|---|
| Markets | All markets |
| Visibility | Public |
| Pricing | Free |
| Free trial | No free trial |

## Properties

| Field | Value |
|---|---|
| Category | Productivity |
| Subcategory | (none) |
| Privacy policy URL | https://github.com/Jsingh-26/Wordwright#privacy |
| Website | https://github.com/Jsingh-26/Wordwright |
| Support contact info | *(maintainer: a support email, e.g. one on unboundkite.com once the domain is set up)* |
| Product declarations | Leave all unchecked. Tick "This app has been tested to meet accessibility guidelines" only after the Accessibility Insights and screen-reader pass in P10.2 has actually been run (zero unnamed controls alone is not that test) |
| System requirements | Windows 11 (x64; the package floor is 10.0.22000). Keyboard: required. Mouse: recommended. Memory: 4 GB recommended |

## Age ratings (IARC questionnaire)

Category **Utility, productivity, communication, or other**. Answers: no violence, no sexual content, no gambling, no user-to-user communication, no sharing of user location, no purchase of digital goods, **no collection of personal information**. Expected rating: 3+ / Everyone.

## Packages

Upload `Releases/Wordwright-1.0.0.msixbundle`. Device family: **Desktop** only.

`runFullTrust` is a restricted capability. If Partner Center asks why it is needed, paste:

> Wordwright is a text expander. It watches the keyboard through a low-level keyboard hook to notice when the user types a snippet shortcut, then pastes the snippet into the active app through the clipboard and SendInput. Both need a full-trust desktop process. Typed text is held only in memory (at most the last 64 characters), is never written to disk or sent anywhere, and the app makes no network connections at all.

`unvirtualizedResources` is no longer declared. Microsoft refused it in the first certification (2026-10-07, policy 10.6.3), so the Store build keeps its data in its own package folder instead (docs/PLAN.md P14.1). Do not paste a justification for it.

## Store listing (English, United States)

**Description**

> Wordwright is a text expander for Windows. Type a short shortcut like ;sig or ;date and it turns into your full text, in any app: Word, Outlook, your browser, chat, even the Windows search box.
>
> Save your signature, addresses, standard replies, code snippets, anything you type again and again. Variables fill themselves in as a snippet expands: today's date, the time, what's on your clipboard, and where the cursor should land.
>
> Private by design. Wordwright makes no network connections at all: no account, no sync, no telemetry, no cloud. Nothing you type is stored, your clipboard is put back after every paste, and your snippets live in one folder on your PC. You can export and import them any time.
>
> It sits quietly in the tray, starts with Windows if you want it to, and stays silent in apps you choose, such as your password manager.
>
> Free and open source (MIT), by Unbound Kite.

**What's new in this version**

> First release on the Microsoft Store. New since the last GitHub release: works with German, French, Spanish and UK keyboard layouts; sharp on mixed-scaling monitors; safer, faster clipboard handling; keeps working after sleep and lock; the editor never loses an edit.

**Product features** (one per line)

- Type a shortcut, get your full text, in any app
- {date}, {time}, {clipboard} and {cursor} variables
- No limit on how many snippets you keep or how long they are
- Searchable snippet list with an editor that saves as you type
- Export and import snippets as JSON
- Turn it off in chosen apps, such as password managers
- No network access, no account, no telemetry
- Your clipboard is restored after every paste
- Light, dark or system theme
- Free and open source

**Search terms** (up to 7): text expander · snippets · typing · autotext · text replacement · productivity · shortcuts

**Short description** (shown on Xbox and some Windows surfaces): A private, offline text expander for Windows.

**Copyright and trademark info:** © 2026 Jaspreet Singh (Unbound Kite). Matches `LICENSE`; Unbound Kite is a brand name, not a registered company.

**Additional licence terms:** MIT licence, https://github.com/Jsingh-26/Wordwright/blob/main/LICENSE

## Images

| Asset | Requirement | Source |
|---|---|---|
| Screenshots (1–10) | PNG, at least 1366×768 | `packaging/store/screenshots/`, in file order: welcome mid-expansion, editor, Settings, About, editor in dark theme (2026-10-03, retaken by `E2E.exe --screenshots`; example names and places are made up) |
| Store logo | 1:1, 300×300 or larger | `brand/icon-256.png` re-rendered at 300 px by `scripts/IconGen` if Partner Center asks for it; otherwise the package's own tiles are used |
| Hero image (optional) | 1920×1080 | `brand/hero.svg` on white |

## Submission notes for certification

> Wordwright is a tray app. After install it starts to the notification area; the window opens from the tray icon or by launching it again from Start. To test: open Notepad and type ;date — today's date replaces it. The app makes no network connections.
