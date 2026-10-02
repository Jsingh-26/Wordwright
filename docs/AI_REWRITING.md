# Offline AI rewriting — planned, built, and parked

**Wordwright was originally built as a text expander *plus* an on-device AI writing
assistant.** That second half was designed, implemented, released three times, and
made to work. It is **not part of the application that ships**. This document is the
record of what it was, how it was built, why it stopped, and how to pick it back up.

If you are reading this because you want the AI feature back: everything is on the
`ai-rewriting` branch, and this page tells you what you are looking at.

---

## The decision (2026-10-02)

Wordwright ships as **a simple Windows text expander**, aimed at the Microsoft Store.

The AI feature was not dropped because it failed or because it was too hard. It was
dropped because **it could not be tested properly**, and shipping it untested would
have been a guess:

- It needs a machine with enough **free memory** to load a multi-hundred-megabyte
  model while Windows is running normally.
- Judging it honestly needs **many devices** — different CPUs, GPUs, memory sizes —
  to know what actually runs acceptably and how fast.
- It also needs the model evaluation in `EVAL.md` to be run to completion before any
  model can be offered to a user.

Neither available machine could supply that: the 16 GB laptop had about **2 GB free**
against a 2.5 GB requirement, and the 4 GB build laptop can never run a catalog model
at all — that is documented behaviour, not a bug (`PLAN.md` → testing machines).

A text expander, by contrast, is testable everywhere. So the AI half was parked
rather than shipped on hope.

**The intent is to bring it back in a future version**, once there is a device to
validate it on.

---

## Where the work is

| What | Where |
|---|---|
| The complete AI implementation | branch **`ai-rewriting`**, at commit **`f01633c`** |
| Everything up to and including it | `main`'s history up to `f01633c` |
| Builds that contained it | releases **v0.1.3**, **v0.1.4**, **v0.1.5** |
| The plan as it stood | `docs/PLAN.md` **on that branch** — phases P4–P9 |
| The model catalog | `models/models.json` on that branch |
| The evaluation harness | `eval/` on that branch |
| The reasoning, in the open | issues [#2](https://github.com/Jsingh-26/Wordwright/issues/2) and [#3](https://github.com/Jsingh-26/Wordwright/issues/3) |

To see it:

```bash
git checkout ai-rewriting
dotnet build -c Release && dotnet test -c Release
```

---

## What it was

Select text in any app, press a hotkey, and a model running **entirely on the PC**
rewrites it in place — no account, no API key, no cloud. Six built-in actions (fix
grammar, make it clearer, more formal, more friendly, shorten, custom instruction),
each able to carry its own hotkey, with a palette that appears near the caret.

The privacy promise was absolute: no telemetry, nothing typed or selected written to
disk or to a log, and the only network access in the entire product was the model
download the user explicitly agreed to.

---

## How it was built

Six phases, each with acceptance criteria and a human check, all recorded in
`PLAN.md` on the branch.

| Phase | What it delivered |
|---|---|
| **P4** | Hardware probe (RAM, CPU, AVX2, GPUs, disk) and a tier classifier — `gpu` / `cpu16` / `cpu8` / `minimal` — with a recommender and a speed estimator, so the app could tell a user what their PC could run and how long a rewrite would take. |
| **P5** | The consent dialogue with a time ruler, and a downloader doing HTTP Range resume into `.part`, SHA-256 verification, atomic rename, free-space checks; plus "import a GGUF by hand". |
| **P6** | The rewriting engine itself: `LLamaSharp` behind a `LocalModel` wrapper (CPU or Vulkan), a prompt builder, an output cleaner with a rule per failure mode a small model produces, the action store and editor, hotkey registration with in-use and duplicate detection, the action palette near the caret, the progress pill with a ruler tick, Esc cancellation, and the snippet picker. |
| **P7** | *Not built.* Calibration (measuring the real speed on the user's machine) and the Offline AI management page. |
| **P8** | *Not built.* The "check for a better model" updater. |
| **P9** | The evaluation harness: 48 test cases, a Python port of the output cleaner carrying the C# tests over rule for rule, automatic checks, a blind two-judge scoring pass, a human spot check, and a report generator. Plus a weekly workflow that opens an issue when a publisher releases a new GGUF. |

Two things are worth calling out because they were the hard parts:

- **The output cleaner.** Small local models leak preambles, wrapping quotes, code
  fences, thinking blocks and trailing notes. Every cleaning rule has a unit test,
  because a rule that silently misses is a bad paste into someone's email.
- **The resource eligibility rule.** A model must actually fit: available RAM ≥ the
  model's requirement + 1 GB, and free disk ≥ file size × 1.2. It is checked at
  startup, before a download starts *or resumes*, before an import, and before a
  model loads — and it judges the specific model, never "something small fits".

---

## What was proven, and what never was

**Proven:**

- The solution builds with **0 warnings and 0 errors**, and the Core suite passed
  **277 tests** at the parked commit.
- **Three releases shipped** containing it: v0.1.3, v0.1.4, v0.1.5.
- On the 16 GB laptop, a **real GGUF was imported and verified** — magic bytes,
  SHA-256 against the catalog, the installed-model record and the file surviving a
  restart — with the model downloaded by hand from the maintainer's own consent.
- The **hardware probe agreed with Windows' own readings** (RAM, CPU, GPU, disk,
  OS build).
- **Invalid-import rejection** and the **hotkey-owned-by-another-app warning** were
  both observed working in the installed app.
- The catalog's five models were filled from Hugging Face with **real sources, sizes
  and SHA-256 hashes**, each confirmed to serve without an account and to begin with
  the GGUF magic.

**Never proven — and this is the honest core of why it stopped:**

- **A single live inference.** No rewrite was ever run end to end on a real machine.
  Everything downstream of "the model loads" is untested.
- Physical hotkey behaviour (`Ctrl+Alt+Space`, `Ctrl+Alt+G`) — automation produced no
  palette and it could not be called either a pass or a defect.
- Rewriting in the real apps (Word, Outlook, Chrome, Teams), `Ctrl+Z`, Esc
  cancellation, clipboard restoration, the elevated-window fallback, and the
  no-user-text-in-logs check.
- The in-app download path, because no catalog model was ever approved.
- Anything about speed or model quality, on any device.

---

## What shipping it would still require

1. **A machine that can actually run it** — enough free memory under normal use, on
   more than one hardware tier.
2. **Run the evaluation** (`eval/README.md` on the branch) and approve models on
   evidence rather than estimate. Until that happens the app correctly refuses to
   offer any model.
3. **Calibration** (P7.1) so the time estimate becomes a measurement.
4. **The Offline AI page** (P7.2) for changing, removing and turning off a model.
5. **The better-model check** (P8) if it is still wanted.
6. **Store considerations**: a multi-hundred-megabyte download from inside a Store
   app, plus the privacy description for a feature that reads selected text.

---

## If you pick this up again

1. `git checkout ai-rewriting` and read `docs/PLAN.md` there — it carries the phase
   list, the acceptance criteria, the human checks and the open questions as they
   stood.
2. `dotnet build -c Release && dotnet test -c Release` to confirm the baseline.
3. Read `docs/MODELS.md` and `docs/EVAL.md` on that branch: the tier rules, the fit
   rule, and the model evaluation method.
4. The two issues named above hold the validation evidence and the review findings,
   including the four resource-guard defects that were found by review and fixed.
5. `git diff main ai-rewriting` shows exactly what the AI feature added — that diff
   *is* the feature.
