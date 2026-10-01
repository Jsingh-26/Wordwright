# Wordwright

[github.com/Jsingh-26/Wordwright](https://github.com/Jsingh-26/Wordwright)

A free, open-source text expander for Windows with a planned AI writing assistant that runs entirely on your own computer.

**Current release: [v0.1.2](https://github.com/Jsingh-26/Wordwright/releases/tag/v0.1.2).** Snippets and model import/download handling are implemented. No catalog model is approved yet, so automatic model download is unavailable. **AI rewriting and calibration are not implemented**; the AI actions and Offline AI pages are placeholders. The AI workflow below describes the planned product.

- **Snippets.** Type `;sig` and your signature appears, in any app. No limits on how many snippets you keep or how long they are.
- **Planned: rewrite anything with a hotkey.** Select text and press `Ctrl + Alt + G` to fix grammar instantly, or `Ctrl + Alt + Space` to pick from *Make it clearer*, *Shorten*, *More formal* and more. Give any action its own hotkey. The text is rewritten in place.
- **Private by design.** The AI model runs on your device. No account, no API key, no cloud. Your text never leaves your PC.

> A *wright* is a maker: a shipwright builds ships, a wheelwright builds wheels. Wordwright builds your words.

## Planned offline AI workflow

1. Snippets work straight after install. The AI features stay off until you turn them on.
2. When you click **Turn on offline AI**, Wordwright checks your computer (memory, processor, graphics card, disk space) and recommends one model that fits it.
3. It tells you, before downloading anything, roughly how long a rewrite will take on your machine and what the model is good and weak at.
4. Only if you agree does it download the model, once. After that everything runs offline.
5. A short speed test on your machine replaces the estimate with your real numbers.

Internet is used only when you choose to download a model, or to check for a better one if you switch that on. Text processing never uses the internet.

## Status

P0–P5 implementation tasks are complete; P6 on-device rewriting is next after the preceding human checks are confirmed. See the current handoff and verification gates in [`docs/PLAN.md`](docs/PLAN.md), and the [Windows test report](docs/WINDOWS_TEST_2026-10-01.md).

## Documentation

| File | What it covers |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Rules for AI coding agents working in this repo |
| [`docs/PLAN.md`](docs/PLAN.md) | Build plan, current handoff, verification gates and acceptance criteria |
| [`docs/WINDOWS_TEST_2026-10-01.md`](docs/WINDOWS_TEST_2026-10-01.md) | v0.1.2 installation and AI setup findings; tested and untested paths |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Projects, components, data files, key technical decisions |
| [`docs/DESIGN.md`](docs/DESIGN.md) | Visual design system, logo system and screen layouts |
| [`brand/`](brand/) | Logo and icon files, hero illustration brief |
| [`docs/UX_COPY.md`](docs/UX_COPY.md) | Every piece of user-facing text |
| [`docs/MODELS.md`](docs/MODELS.md) | Model catalog, hardware tiers, speed estimates |
| [`docs/EVAL.md`](docs/EVAL.md) | How models are chosen: the evaluation method |

## License

MIT. Downloaded AI models keep their own licenses, shown before download.
