# Microsoft Store release plan (v1.0.0)

Written 2026-10-03. The aim is to publish Wordwright in the most stable form possible. A Store-built package goes to you privately first, is tested end to end, and only then goes public. Each step names who does it and when it counts as done. **You** means the maintainer; **Claude** means the agent. [STORE_LISTING.md](STORE_LISTING.md) holds the text to paste, and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) holds the test results.

## Where things stand

Done on 2026-10-03:

- **Font history purge.** Both font files are purged from every branch and tag and force-pushed. CI is green on the new history.
- **Windows 11 only.** The package floor is 10.0.22000. README and the Store listing say Windows 11. About says what v1.0 was tested on.
- **Seeded signature.** The example signature no longer ships with the maintainer's name. It says "Your name".
- **Snippet text box.** It no longer justifies its text, a WPF-UI default.
- **Store screenshots.** Five are in `packaging/store/screenshots/`, all at least 1366×768: the welcome window mid-expansion, the editor, Settings, About, and the editor in dark theme. `E2E.exe --screenshots` retakes them on any build.
- **End-to-end tests.** 27 of the 29 checks pass on the plain build. The two misses are start-up time (R4) and two apps that failed only because another window kept the foreground.

Not done: anything that needs your Microsoft account, and any test of the **Store package** itself.

## Decisions for you (answer before Phase 1 ends)

| Decision | Recommendation | Why |
|---|---|---|
| Individual or company developer account | **Individual** | Free, and you can open it today. A company account needs a registered legal business, and Unbound Kite is not registered yet. You cannot change an Individual account into a Company one later. You would open a new Company account when the business exists and move the app then. |
| Publisher display name | The name Partner Center lets you use | An individual account publishes "under your own name". If Partner Center does not accept "Unbound Kite", the manifest, the Store listing and the About line ("by Unbound Kite") change to match. That is one string each, and Claude does it. |
| First submission to a private audience | **Yes** | Only you see and install it, from a private link. You can choose Private only *before* the app is ever public. |

## Phase 1: Developer account (You, about 30 minutes) — done 2026-10-03, Individual, publisher "Unbound Kite"

1. Go to **storedeveloper.microsoft.com**, choose "Get started for free", then **Individual developer**. This is the only route without a fee; Partner Center's own sign-up page uses the old paid flow.
2. Sign in with the Microsoft account you want to own the app.
3. Verify your identity: a government ID and a selfie, taken on your phone.
4. Finish the profile and click "Go to Partner Center dashboard". If Apps and games doesn't show, wait 5 minutes and refresh.

Done when: the Partner Center **Apps and games** page opens. Claude cannot do this part: it creates an account and verifies your identity.

## Phase 2: Reserve the name and get the identity (You sign in; Claude drives) — done 2026-10-03

Before this phase, connect the browser. Install the Claude in Chrome extension (https://chromewebstore.google.com/detail/fcoeoabgfenejglbffodgkkbkcdhcgfn), open its side panel and sign in with the same Claude account. It was not connected on 2026-10-03. The fallback is that you sign in to Partner Center inside the Claude app's own browser pane.

1. Claude: Apps and games → New product → **MSIX or PWA app** → name **Wordwright** → check availability. **You** click *Reserve product name*.
2. Claude reads **Product management → Product identity**. It holds three values: `Package/Identity/Name`, `Package/Identity/Publisher` (starts `CN=`) and `Package/Properties/PublisherDisplayName`.

Done when: the name is reserved and the three values are in this file's *Identity* section below.

## Phase 3: Build the Store package (Claude)

1. Put the three identity values into `packaging/AppxManifest.xml`. If the publisher display name is not "Unbound Kite", change `About.Publisher` in UX_COPY.md and the resource file, STORE_LISTING.md and README to match.
2. Add an execution alias (`uap5:AppExecutionAlias`, `wordwright.exe`) to the manifest. The end-to-end runner then starts the Store-installed app with its test-mode variable; a packaged app started through the shell doesn't inherit environment variables. Users never notice it.
3. Rebuild: `scripts/pack-msix.ps1 -Version 1.0.0`. Check that the manifest in the bundle carries the new identity and `MinVersion 10.0.22000.0`.
4. **Windows App Certification Kit.** Add a CI job that installs the unsigned package on the windows-latest runner, which is admin, and runs `appcert.exe` on it. The kit is not on this laptop, and it needs administrator rights. Fix anything it reports.
5. Run the full end-to-end suite on the final build. Record the results in RELEASE_CHECKLIST.md with the commit.
6. Optional, decided by the numbers: P13.5 start-up. Try `PublishReadyToRun` and keep it only if R4 drops noticeably on this laptop. It is a polish item, not a gate.

Done when: CI is green with the certification kit passing, the E2E report shows no app failures, and `Releases/Wordwright-1.0.0.msixbundle` carries the real identity.

*Progress 2026-10-03:* steps 1–4 done. The manifest carries the Partner Center identity and the `wordwright.exe` alias. `Releases/Wordwright-1.0.0.msixbundle` (75 MB) was rebuilt and its manifest checked: `UnboundKite.Wordwright` 1.0.0.0, floor 10.0.22000. The new CI job `certify` signs the package with a throwaway test certificate and runs the Windows App Certification Kit 10.0.26100 on Windows Server 2025. **Overall PASS** (run 37116875954): 23 of 24 tests pass. The 24th, "Blocked executables", is marked optional and lists `Process.Start`, `ShellExecute` and `cmd`/`reg` string references inside the bundled .NET, WPF and library DLLs. Every self-contained .NET desktop app shows it, so it is accepted for full-trust apps. Still open: step 5 (full E2E run on this build) and step 6 (optional).

## Phase 4: First submission, private audience (Claude fills; You submit)

Partner Center → Wordwright → Start your submission. All text is in STORE_LISTING.md.

| Page | What goes in |
|---|---|
| Pricing and availability | Markets: all. **Visibility: Private audience.** New known user group "Wordwright testers" with your Microsoft account's email. Price: free. |
| Properties | Category Productivity. Privacy policy URL `https://github.com/Jsingh-26/Wordwright#privacy`; Claude checks it opens on the pushed README first. Support contact: the GitHub issues URL. System requirements: keyboard required, Windows 11. |
| Age ratings | The IARC questionnaire answers in STORE_LISTING.md: no violence, no user interaction, no purchases, no data collection. |
| Packages | Upload `Wordwright-1.0.1.msixbundle`. If asked, paste the restricted-capability note for `runFullTrust`. (`unvirtualizedResources` was refused and removed; see Submission 2 below.) |
| Store listing (English, US) | Description, What's new, features, keywords. The five screenshots from `packaging/store/screenshots/`, light first. |
| Submission options | Notes for certification: the paragraph in STORE_LISTING.md. It says the app is a tray app, to type `;date` in Notepad, and that it makes no network connections. |

**You** click *Submit to the Store*. Certification usually takes up to three business days. Claude checks the status. If certification fails, Claude reads the report, fixes, rebuilds and prepares a resubmission.

Done when: the submission is *In the Store* for the private audience.

*Progress 2026-10-03 (maintainer's choice: public, link only, not private):* Submission 1 is filled in and every section reads Complete:
- **Pricing and availability:** all 240 markets; Public audience; "available but not discoverable", **direct link only**; free (USD 0).
- **Properties:** Productivity. Personal information: **Yes**, because the keyboard hook reads typed text even though nothing is collected or sent. Privacy policy: README#privacy. Website: the GitHub repository. Support: GitHub issues. Keyboard required, 4 GB recommended.
- **Age ratings:** IARC questionnaire, all No; 3+ / Everyone (ESRB Everyone, PEGI 3, USK Everyone). The maintainer accepted the IARC terms.
- **Packages:** `Wordwright-1.0.0.msixbundle` (75 MB) **Validated**; Desktop only; "let Microsoft decide future device families" unticked.
- **Store listing (en-US):** description, ten features, five screenshots, seven keywords, short description, copyright, MIT licence terms, "Developed by Unbound Kite". "What's new" is blank, as Microsoft asks for a first submission.
- **Submission options:** publish as soon as certified. The restricted-capability explanations for `runFullTrust` and `unvirtualizedResources` (the second shortened to under 500 characters). Testing notes on the Additional Testing Information page.

The Partner Center pages were filled through Claude in Chrome. The 75 MB package and the screenshots went through the chrome-devtools browser, because Claude in Chrome caps uploads at 10 MB. **Submit for certification is the maintainer's click.**

*Certification 2026-10-07: Attention needed.* Report: policy 10.6.3 Capabilities, "Your request to use unvirtualizedResources has been reviewed and was denied". Nothing else was flagged. Decision (Claude, P14.1): remove the capability rather than ask again. Its only purpose was to let the Store build share the real `%AppData%\Wordwright` with the installer build; a fuller justification would add nothing new, and the report warns that a repeat request without new information gets the same answer. The Store build now keeps its data in its package's own `LocalState` folder and copies the installer build's snippets on its first run.

**Resubmitted 2026-10-07 (maintainer asked Claude to publish):** Partner Center refused the rebuilt 1.0.0 bundle because its package full name `UnboundKite.Wordwright_1.0.0.0_x64` had already been uploaded with different contents, so it was rebuilt with `scripts/pack-msix.ps1 -Version 1.0.1`. `Wordwright-1.0.1.msixbundle` (75 MB, bundle version 2026.1007.1802.0, declares only `runFullTrust`) was uploaded through the chrome-devtools browser, the 1.0.0 package was removed, and Packages reads **Validated**; its validation warning now names only `runFullTrust`. Submission options lists only `runFullTrust`, with the existing note. Resubmitted for certification: status *In certification*.

## Phase 5: Test the real Store build (Claude, with you clicking Install once)

1. **You:** open the private Store link while signed in with the tester account, and click *Get*. The Store installs it; this is the build users will get.
2. **Claude:** run the end-to-end suite against it through the alias: `E2E.exe --exe wordwright.exe`. The Store build's own checks are:
   - **I4:** the packaged startup task. Turn "Start Wordwright when I sign in" on, sign out and in. You sign in, then Claude verifies.
   - **I5:** Open data folder shows the package's `LocalState`, holding `snippets.json`; snippets made in the installer build appear on the Store build's first run. The toggle matches Task Manager → Startup apps after you turn it off there.
   - **I2:** uninstall removes the package folder and leaves nothing else; `%AppData%\Wordwright` is untouched. Claude compares the folders and `HKCU\Software` before and after.
3. Fix anything found, then resubmit to the private audience and repeat until clean.

Done when: every E2E check passes against the Store build, and I2, I4 and I5 are recorded.

## Phase 6: The checks that need a Windows setting (You flip; Claude runs; about 20 minutes)

The runner does not change Windows settings on your PC. For each row: you change the setting, Claude runs the listed checks and looks at the captures, then you change it back.

| Row | You set | Claude runs |
|---|---|---|
| S1 | Settings → Accessibility → Contrast themes → Night sky, then Desert | `--only S2a,S8` plus captures |
| S2 | Display → Scale 100 %, then 200 % (and 300 % if offered) | `--only S2a,S5,S6` |
| S7 | Personalization → Colors → Windows mode **Light** | `--only S7,S7a` |
| S9 | Accessibility → Visual effects → Animation effects **off** | `--only S8,I3a` and a capture check that nothing moves |
| S3, S4 | Optional: a second monitor, or 1366×768 resolution | `--only S2a,S5` |

Done when: these rows read Pass in RELEASE_CHECKLIST.md, or n/a with a reason.

## Phase 7: Go public (Claude prepares; You click)

1. **Claude:** README GIF (P10.1), recorded from the welcome try-it box with the runner's capture code. Release notes for v1.0.0 are already in `docs/RELEASE_NOTES_v1.0.0.md`. Update them with today's fixes.
2. **Claude:** GitHub release **v1.0.0** (P10.3): tag, notes, `Wordwright-win-Setup.exe` and the portable zip, built from the same commit as the Store package. **You** confirm before Claude publishes it.
3. **You:** new submission in Partner Center that changes Visibility to **Public audience**. Claude fills it; you click Submit. **This cannot be undone**: the app can never return to a private audience.
4. **Claude:** after it is live, open the public listing, install from it on this laptop, and run the E2E suite one last time.

Done when: the listing is public, the GitHub release is out, and the final E2E run is green.

## Phase 8: After launch

- **You:** GitHub Support request (support.github.com, "Remove sensitive data") to remove `refs/pull/1/head` and the cached font commits. Paste: *Please remove sensitive data from Jsingh-26/Wordwright. I rewrote history to remove two font files whose licence forbids redistribution (src/Wordwright.App/Resources/Fonts/Zodiak-Bold.otf and Zodiak-Regular.otf). Please remove the pull request ref refs/pull/1/head (PR #1, commit 7c2975e) and cached views of commits e1f06fa2b620412c27dac4cebc12590f9e0f24bb and 0e91590c1995a595210b9973f36f5bf31bb873c1.*
- **You:** re-clone the repository on the 16 GB laptop. Don't pull into the old copy.
- **Claude:** watch Partner Center's reviews and the Store's own crash reports. Wordwright has no telemetry, so these are the only signal.
- **Both:** pick v1.1 from D14–D25 in PLAN.md (P13.10).

## Identity (filled in Phase 2)

| Field | Value |
|---|---|
| Package/Identity/Name | `UnboundKite.Wordwright` |
| Package/Identity/Publisher | `CN=785E0A85-0C77-4BFB-8871-297326220CCD` |
| Package/Properties/PublisherDisplayName | `Unbound Kite` |
| Store ID | `9PP6SR2R3FS3` (https://apps.microsoft.com/detail/9PP6SR2R3FS3; package family `UnboundKite.Wordwright_y6mjg7tfeg67g`). Reserved 2026-10-03: submit by 2027-01-03 or the name is released |

## Sources (opened 2026-10-03)

- learn.microsoft.com … open-a-developer-account: a free individual account via storedeveloper.microsoft.com, ID and selfie verification, and Individual cannot be changed to Company.
- learn.microsoft.com … msix/visibility-options: Private audience for beta testing, set before the app is ever public; testers need a personal Microsoft account.
- learn.microsoft.com … msix/app-package-requirements: the Store re-signs packages, so the upload stays unsigned; test with the Windows App Certification Kit.
