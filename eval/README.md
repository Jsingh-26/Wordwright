# The evaluation harness

This is how Wordwright chooses its models with measurements instead of guesses
(`docs/EVAL.md` is the specification; this file is how to run it). **Nothing
here ships with the app.**

Requires Python 3.11+ and a local [llama.cpp](https://github.com/ggml-org/llama.cpp)
build with `llama-server`.

```bash
python -m pip install -r eval/requirements.txt
```

## Files

| File | What it does |
|---|---|
| `cases/*.jsonl` | The 48 test cases: 8 each for `fix`, `clear`, `formal`, `friendly`, `shorten` and 8 `custom` instructions. |
| `common.py` | Case loading, the exact prompt wording, and the generation limits, all copied from the app. |
| `cleaner.py` | Port of `OutputCleaner` (`src/Wordwright.Core/Actions/OutputCleaner.cs`). |
| `tests/test_cleaner.py` | The same tests as `OutputCleanerTests.cs`, so the port cannot drift silently. |
| `checks.py` | The four automatic checks; a failure caps that case's score at 2. |
| `run_candidates.py` | Runs every case against one candidate model through `llama-server`. |
| `judge.py` | Scores the outputs with two judge models, blind, and averages them. |
| `spotcheck.py` | Shows you ~10% of the outputs to rate yourself, then reports judge–human agreement. |
| `report.py` | Writes `results/REPORT.md`, the evidence behind `models/models.json`. |

`results/` is git-ignored: it holds raw outputs and scores, not source.

## Running it

Test the cleaner port first — it needs no model:

```bash
python -m pytest eval/tests -q
```

Run one candidate. The script can start and stop `llama-server` for you, using
the exact GGUF and the app's context size, threads and backend:

```bash
python eval/run_candidates.py --model qwen3.5-2b-q4km \
    --llama-server C:/tools/llama.cpp/llama-server.exe \
    --model-path C:/models/qwen3.5-2b-q4km.gguf --threads 11
```

Or point it at a server you started yourself:

```bash
llama-server -m qwen3.5-2b-q4km.gguf -c 4096 --port 8080
python eval/run_candidates.py --model qwen3.5-2b-q4km --endpoint http://127.0.0.1:8080
```

Score every model that has been run:

```bash
export JUDGE_BASE_URL=https://ollama.com/v1
export JUDGE_API_KEY=...
export JUDGE_MODELS=kimi-k3,glm-5.3
python eval/judge.py
```

Judge replies are cached per output in `results/judged/`, so a rerun after a
network drop does not pay for the same scoring twice.

Rate a sample yourself and check the judges agree with you:

```bash
python eval/spotcheck.py
```

Finally, write the report:

```bash
python eval/report.py
```

## Speed

Quality is half of it. For the other half, record per machine — CPU, RAM, GPU,
backend, prompt tok/s, generation tok/s and load time — in
`results/speed.csv` (columns: `machine,cpu,ramGB,gpu,backend,tier,model,promptTps,genTps,loadSeconds`),
using Wordwright's own calibration numbers from a debug build or `llama-bench`
with the same threads and backend. `report.py` turns those rows into the
suggested `speed` ranges for the catalog: observed min–max, widened by 15%, and
marked `provisional` until at least two machines per tier have been measured.

## Things to know before trusting a number

- **The harness cleans exactly as the app does.** If `cleaner.py` and the C#
  `OutputCleaner` ever disagree, a score here would not mean what a paste in
  Notepad means. Change both, and the tests, together.
- **`max_tokens` is estimated from a ~4 characters per token rule.** The app
  counts with the model's own tokenizer. The only place this matters is the cap
  on a runaway generation, which is generous either way.
- **The length check is applied to `custom` cases too**, because
  `docs/EVAL.md` asks for 0.6–1.6 × on every action except `shorten`. A custom
  instruction such as "Summarise this in three sentences" is legitimately far
  shorter than its input, so those cases can fail this check by the design of
  the case rather than by a fault in the model — see the comment in
  `checks.py` if custom instructions should be exempt.
- **A failed automatic check caps a case at 2.** That is deliberate: an output
  that loses a name or leaks a preamble should not score well on style alone.
