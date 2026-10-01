# 16 GB laptop validation — 2026-10-01–02

GitHub handoff: https://github.com/Jsingh-26/Wordwright/issues/2

Validation on the maintainer's 16 GB laptop, 2026-10-01–02 (Asia/Kolkata).

**Outcome: partial validation; P4/P5/P6 are not fully confirmed and P7 is not cleared.** This is an agent-operated desktop test, with observed UI results separated from automated tests and checks needing physical input or a real model.

## Baselines
- Source: main `e2390b3a45663b6ae1ced8b7bd8ca475b4eea523`.
- Installed release: v0.1.3, built from `7c2975e`. Installer SHA-256 matched the GitHub asset digest `1142d4774aab361f30b93830b54b04a6212581d67f1f2740ba2fdba0ac7859ce`; silent installer exit 0; installed About page showed 0.1.3.
- Installation/data paths are redirected into the Codex desktop package's LocalCache. This does not establish installation under a clean ordinary Windows user.

## Observed desktop checks
| Check | Result |
|---|---|
| P4 debug Hardware page | Opened successfully: Ryzen 5 7535HS, 6 physical cores, 15.9 GB usable RAM, 0.9 GB available at capture, AVX2 Yes, AVX-512 No, AMD Radeon graphics 1.0 GB, free disk 238.1 GB, Windows 10.0.26200. |
| Independent Windows readings | DxDiag: installed RAM 16384 MB; OS-available total 15134 MB; Ryzen 5 7535HS / 12 logical CPUs; AMD Radeon / dedicated 995 MB; Windows build 26200. GlobalMemoryStatusEx: total 14.7795 GiB (15.8694 decimal GB), free 0.6955 GiB at a later sample. Disk: 221.62 GiB (about 238 GB). Memory availability varies between samples. CPU feature flags and the physical-core count were not separately measured. |
| P4 tier | `cpu8` is consistent with the current rule: usable total RAM is below 16,000,000,000 bytes; GPU dedicated memory is below 6 GB. Keep the documented rule unchanged. A marketed 16 GB laptop does not necessarily reach `cpu16`. |
| P5 consent | Installed release displayed “There's no model ready to offer yet”; Download and turn on disabled; Import model file instead available. Expected for the catalog, which has no approved entries. |
| P5 invalid import | Selected a deliberately invalid text file named `not-a-model.gguf` through the real file picker. App displayed “That file isn't a GGUF model. Pick the .gguf file you downloaded.” No successful import is claimed. |
| P6 AI actions UI | Six built-in entries and grammar instruction/hotkey visible; Try it disabled with the AI-off hint. Offline AI remains a placeholder, as P7.2 is unfinished. |
| P6 hotkey collision | PASS for action warning. A separate controlled process successfully reserved Ctrl+Alt+F12. With that combination assigned to the grammar action in a temporary test profile, the installed app displayed “Ctrl+Alt+F12 is used by another app. Choose a different one.” |
| P6 palette/direct hotkeys | Automation-generated Ctrl+Alt+Space in blank and selected-text Notepad produced no visible palette, in debug and installed builds. Automation-generated Ctrl+Alt+G did not establish a Wordwright result. Physical-key confirmation is still required; do not classify this as a confirmed app defect or a pass. |
| Test cleanup | Original Wordwright profile restored after fresh-profile and collision fixtures; test hotkey reservation stopped. No test fixture remains assigned in the original profile. v0.1.3 remains installed. |

## Automated evidence
- Release and Debug solution builds succeeded with zero warnings/errors.
- A full Core test run passed 258/258.
- Focused downloader suite passed 12/12, covering controlled interrupted/resumed transfers and integrity checks.
- Focused importer suite passed 10/10.
- Full-suite runs also intermittently failed 1/258 in the localhost HTTP fixture: `ObjectDisposedException: System.Net.HttpListener`, at `TestHttpServer.cs:24`. Failures occurred while constructing different downloader tests (`ADownloadThatVerifies_isPutInPlace` and `AServerWithoutRanges_startsTheFileAgain`). The constructor retries `Start()` using the same listener after `HttpListenerException`; the observed subsequent Prefixes access is on a disposed listener. Investigate/recreate the listener per retry rather than claiming every run is green. No production downloader failure is established by this fixture exception.
- Automated downloader tests do not replace the blocked installed-app download check.

## Remaining checks before P7
1. Free enough RAM for the chosen model plus the documented 1 GB headroom. This session had only roughly 0.8–0.9 GB free.
2. Provide/obtain a genuine suitable GGUF with consent and a recorded source/hash. No GGUF was found in Downloads or the checked Ollama blobs (only small metadata blobs); other locations were not exhaustively searched. No model downloaded or imported in this session.
3. Import through v0.1.3, verify installed-model record and file after app restart, then perform actual generation.
4. Physically verify Ctrl+Alt+Space and Ctrl+Alt+G. Finish snippet list/filter “sig”/Enter insertion and Esc unchanged-text checks.
5. Run every built-in action in Notepad, Word, Outlook, Chrome and Teams; verify Ctrl+Z, Esc cancellation, clipboard restoration, elevated-window Copied fallback and absence of user text in logs during real inference. No message sending is needed for these tests.
6. Installed-app download interruption/resume and tamper handling remain blocked by the unapproved catalog. Any staged-validation exception requires an explicit maintainer decision and deferred retest after P9.4.

The other laptop can investigate the HTTP test fixture and continue work explicitly allowed by the existing plan (such as catalog metadata/evaluation prerequisites). It must not treat this report as a P7 gate approval, model approval, completed calibration, or successful live rewriting.


## Follow-up recheck — 2026-10-02

Source synchronized to `bb5b29e` (HTTP fixture retry fix, verified Qwen candidate, evaluation prompt-hint fix and updated handoff).

- Release solution build: **0 warnings / 0 errors**.
- Core suite: **259/259 passed**, including the deterministic occupied-port regression test. The prior disposed-listener fixture failure did not recur in this run.
- Python cleaner suite: **21/21 passed** using an isolated virtual environment under the chat workspace; no repository dependencies were changed.
- Catalog/prompt smoke check: Qwen's `disableThinking` is a string; `/no_think` is appended to the user message and not the system message, matching the current inference implementation.
- Independent hardware evidence: Windows `GetLogicalProcessorInformationEx(RelationProcessorCore)` reports **6 physical cores**. A separate .NET intrinsics probe reports **AVX2 True; AVX512F False**. These match Wordwright's debug page.
- Available RAM at recheck: **1.9468 GiB**, approximately **2.09 decimal GB**.

**RAM correction:** issue #3 previously calculated 1.7 GB from the 639 MB file size. The documented rule actually uses `ramRequiredGB + 1 GB`; the current verified Qwen candidate has `ramRequiredGB: 1.5`, so the required available RAM is **2.5 GB**. The latest sample remains below that threshold. No fit rule or catalog estimate was relaxed.

The installed v0.1.3 remains available for desktop checks. Its embedded catalog predates the new Qwen entry; the latest source catalog change does not make the installed release offer an approved model. Manual-import and actual generation must be observed separately.

Model-download consent and physical Ctrl+Alt+Space confirmation were requested from the maintainer. No response was recorded during this recheck; no download, real-model import/persistence, live rewrite, undo/cancellation/clipboard/elevated fallback, or inference-log privacy pass is claimed. Hardware evidence is now complete for the requested CPU flags/core-count comparison, while maintainer phase confirmation remains outstanding. **P4/P5/P6 gate confirmations stay unticked and P7 is not cleared.**

The [remaining checklist in issue #3](https://github.com/Jsingh-26/Wordwright/issues/3) now marks the installed release and independent hardware check as evidenced, corrects the RAM prerequisite and leaves unperformed checks unchecked.
