# Evaluation: how Wordwright chooses its models

Every catalog model is chosen with measurements, not guesses. The harness lives in `eval/` (Python 3.11+) and is **not shipped** with the app.

## Part A: rewrite quality

### Test set (`eval/cases/*.jsonl`)
- 48 cases: 8 per built-in action (`fix`, `clear`, `formal`, `friendly`, `shorten`) plus 8 `custom` instructions.
- Mix of lengths (one line, short paragraph, ~150-word paragraph), styles (work email, chat message, notes with bullets), and traps: names, numbers, dates, links, line breaks, and a few Hinglish sentences.
- Each line: `{"id", "action", "instruction", "input", "must_keep": [strings that must survive], "notes"}`.
- `eval/cases/cases.jsonl` holds the complete set (`sample.jsonl`, the first 12, was folded into it in task P9.1).

### Running candidates (`eval/run_candidates.py`)
- Runs each candidate **with the exact GGUF file and prompt Wordwright uses** (`docs/ARCHITECTURE.md` → Prompt, Generation parameters), through a local `llama-server` (llama.cpp) on its OpenAI-compatible endpoint. This keeps results faithful to what users get.
- Applies the same `OutputCleaner` rules (port them to Python in `eval/cleaner.py`, with the same tests).
- Saves raw outputs to `eval/results/raw/<model>/<case>.json` (git-ignored) and a summary CSV.

### Automatic checks (`eval/checks.py`), per output
1. `preamble_left`: preamble or notes still present after cleaning.
2. `must_keep_missing`: any `must_keep` string missing.
3. `length_ratio`: `shorten` must be ≤ 0.85 × input; other actions 0.6–1.6 ×.
4. `empty_or_runaway`: rejected by the cleaner.
A failed check caps that case's score at 2.

### Judge (`eval/judge.py`)
- Two judge models via any OpenAI-compatible API, configured by environment variables (`JUDGE_BASE_URL`, `JUDGE_API_KEY`, `JUDGE_MODELS`). During the build, the maintainer uses Ollama Cloud (`https://ollama.com/v1`) with two strong models (e.g. `kimi-k3` and `glm-5.3`); scores are averaged to reduce single-judge bias. Any strong model works later.
- Blind: the judge never sees which model produced an output; order is randomised.
- Rubric, each 1–5: **Meaning kept**, **Instruction followed**, **Correct and natural**, **Clean output** (only the rewritten text). Judge returns strict JSON; retry once on invalid JSON.
- Case score = mean of the four, capped by automatic checks. Model `evalScores.<action>` = mean over that action's cases; `overall` = mean of action scores.

### Human spot check
The maintainer rates a random 10% of outputs with the same rubric (`eval/spotcheck.py` shows them one at a time, blind). Report judge–human agreement (share of cases within ±1). If agreement is below 70%, fix the rubric before trusting scores.

## Part B: speed
- Use Wordwright's own calibration (debug build shows raw numbers) or `llama-bench` with the same threads and backend.
- Record per machine: CPU, RAM, GPU, backend, prompt tok/s, generation tok/s, load time → `eval/results/speed.csv`.
- Catalog `speed[tier]` ranges = min–max observed for that tier, widened by 15%. Keep `provisional: true` until at least two different machines per tier have been measured.

## Output
`eval/report.py` produces `eval/results/REPORT.md`: a table of models × actions with scores, automatic-check failure rates, speed per tier, judge–human agreement, and the chosen default per tier with one sentence of reasoning. Link it from the main README. This report is the evidence behind every entry in `models.json`.
