# Wordwright

[github.com/Jsingh-26/Wordwright](https://github.com/Jsingh-26/Wordwright)

A free, open-source text expander for Windows. Type a shortcut and your text appears, in any app.

**Current release: [v0.2.0](https://github.com/Jsingh-26/Wordwright/releases/tag/v0.2.0).** Snippets, and nothing else: no account, no sign-in, no network access of any kind.

- **Snippets.** Type `;sig` and your signature appears, in any app. No limit on how many you keep or how long they are.
- **Variables.** `{date}`, `{time}`, `{clipboard}` and `{cursor}` fill themselves in as the snippet expands.
- **Private by design.** Nothing you type is written to disk or to a log, and the app makes no network connections of its own.
- **Your data, in one place.** Everything lives in `%AppData%\Wordwright`, and your snippets export and import as JSON.

> A *wright* is a maker: a shipwright builds ships, a wheelwright builds wheels. Wordwright builds your words.

## What is not here, and why

An earlier Wordwright also had an **on-device AI writing assistant**: select text, press a hotkey, and a model running entirely on your PC rewrote it in place. It was built, it worked, and three releases contained it.

It is not part of this application. It needs a machine with enough free memory to load a model, and a range of devices to judge speed and quality on — neither of which was available to test it properly. Rather than ship an untested feature that downloads a multi-gigabyte model, it was parked.

**All of it is preserved**, and the whole story — what was planned, how it was built, what was proven and what never was, and how to pick it up again — is in [`docs/AI_REWRITING.md`](docs/AI_REWRITING.md). The code is on the [`ai-rewriting`](https://github.com/Jsingh-26/Wordwright/tree/ai-rewriting) branch, and releases **v0.1.3–v0.1.5** contain it.

## Status

Snippets only, and heading for the Microsoft Store. The plan and what is left are in [`docs/PLAN.md`](docs/PLAN.md).

## Documentation

| File | What it covers |
|---|---|
| [`docs/AI_REWRITING.md`](docs/AI_REWRITING.md) | The parked AI writing assistant: what it was, how it was built, why it stopped, how to resume |
| [`AGENTS.md`](AGENTS.md) | Rules for AI coding agents working in this repo |
| [`docs/PLAN.md`](docs/PLAN.md) | Build plan, current handoff and acceptance criteria |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Projects, components, data files, key technical decisions |
| [`docs/DESIGN.md`](docs/DESIGN.md) | Visual design system, logo system and screen layouts |
| [`docs/UX_COPY.md`](docs/UX_COPY.md) | Every piece of user-facing text |
| [`brand/`](brand/) | Logo and icon files |

## License

MIT.
