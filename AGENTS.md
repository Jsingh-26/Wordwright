# AGENTS.md — rules for AI coding agents

You are building **Wordwright**, a Windows tray app: a text expander plus an offline, on-device AI rewrite tool. Read these files before writing any code, in this order:

1. `docs/PLAN.md` — what to build, in which order, and how each task is judged done
2. `docs/ARCHITECTURE.md` — projects, components, data formats, technical decisions
3. `docs/DESIGN.md` and `docs/UX_COPY.md` — how it looks and exactly what it says
4. `docs/MODELS.md` — model catalog and hardware tiers

## How to work

- Work on **one task at a time**, in the order given in `docs/PLAN.md`. Each task has an ID like `P2.3`.
- Before starting a task, restate its acceptance criteria in one short paragraph, then build only that.
- After each task: run `dotnet build` and `dotnet test` from the repo root. Both must pass with zero warnings treated as errors in `Wordwright.Core`.
- Tick the task's checkbox in `docs/PLAN.md` and commit with the message `P2.3: <short summary>`.
- Do not start the next phase until every task in the current phase is ticked and the phase's **human check** has been confirmed by the maintainer.
- If a decision is not covered by the docs, **stop and ask**. Do not invent product behaviour.
- Prefer small, boring, readable code over clever code. No new dependencies beyond those listed in `docs/ARCHITECTURE.md` without asking.

## Hard rules (never break these)

1. **No network access** anywhere except `ModelDownloader` and `CatalogUpdater` in `Wordwright.Core`/`Wordwright.App`. Both run only after an explicit user action or the opt-in weekly check. No telemetry, analytics, crash reporting or "phone home" of any kind.
2. **Never write user text to disk or logs.** Typed characters, selected text, clipboard contents and model outputs live only in memory. Logs may record events (e.g. "rewrite finished in 3.2 s") but never content.
3. The keystroke buffer holds at most the last 64 characters, in memory, and is cleared on focus change, mouse click, Enter, Escape and navigation keys.
4. Always restore the user's clipboard after a paste operation.
5. Never download a model without the consent dialogue, and never use a downloaded file before its SHA-256 matches the catalog.
6. Never delete a working model until its replacement is downloaded, verified and calibrated.
7. Use only the colours, fonts, icons and logo files defined in `docs/DESIGN.md` and `brand/`. No new colours, fonts or icon sets.
8. All user-facing strings come from `docs/UX_COPY.md` (implemented as a resource file). Do not write new copy inline.
9. `Wordwright.Core` must not reference WPF, WinForms or Win32 APIs, so it stays unit-testable.

## Environment

- Windows 10 (19045+) / Windows 11, x64. ARM64 is out of scope for v1.
- .NET 10 SDK (LTS). If a listed dependency does not support .NET 10, use .NET 8 and note it in `docs/ARCHITECTURE.md`.
- The maintainer runs manual tests on a real Windows machine; ask for a human check where `docs/PLAN.md` says so.
