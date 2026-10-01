"""Run every case against one candidate model (docs/EVAL.md → Running candidates).

The point is fidelity: each model is run with the *exact* GGUF file and the
*exact* prompt and generation limits Wordwright uses, through a local
``llama-server`` (llama.cpp) on its OpenAI-compatible endpoint. Anything that
would be cleaned away by the app is cleaned away here too.

Typical use, letting the script start and stop llama-server for you:

    python eval/run_candidates.py --model qwen3.5-2b-q4km \
        --llama-server C:/tools/llama.cpp/llama-server.exe \
        --model-path C:/models/qwen3.5-2b-q4km.gguf --threads 11

Or against a server you started yourself:

    llama-server -m qwen3.5-2b-q4km.gguf -c 4096 --port 8080
    python eval/run_candidates.py --model qwen3.5-2b-q4km --endpoint http://127.0.0.1:8080

Raw outputs land in ``eval/results/raw/<model>/<case>.json`` (git-ignored) and a
one-row-per-case CSV in ``eval/results/summary.csv``.
"""

from __future__ import annotations

import argparse
import csv
import json
import subprocess
import sys
import time
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))

import common
from checks import run_checks
from cleaner import clean

SUMMARY_FIELDS = [
    "model",
    "case_id",
    "action",
    "accepted",
    "rejection",
    "failed_checks",
    "input_words",
    "output_words",
    "output",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model", required=True, help="catalog id, used to label the run and read promptHints")
    parser.add_argument("--endpoint", default="http://127.0.0.1:8080", help="llama-server base url")
    parser.add_argument("--llama-server", help="path to llama-server; when given the script starts and stops it")
    parser.add_argument("--model-path", help="the .gguf to load when starting llama-server")
    parser.add_argument("--threads", type=int, default=4, help="generation threads (app default: physical cores - 1)")
    parser.add_argument("--gpu-layers", type=int, default=0, help="layers offloaded; 0 is CPU-only")
    parser.add_argument("--limit", type=int, help="only the first N cases, for a smoke run")
    parser.add_argument("--timeout", type=int, default=300, help="seconds to wait for one reply")
    return parser.parse_args()


def catalog_entry(model_id: str) -> dict:
    for model in common.catalog_models(status=None):
        if model.get("id") == model_id:
            return model

    # A hand-imported GGUF has no catalog entry; run it with app defaults.
    return {"id": model_id, "promptHints": {}}


def wait_for_server(endpoint: str, timeout: int = 180) -> None:
    deadline = time.time() + timeout

    while time.time() < deadline:
        try:
            if requests.get(f"{endpoint}/health", timeout=2).status_code == 200:
                return
        except requests.RequestException:
            pass
        time.sleep(1)

    raise TimeoutError(f"llama-server at {endpoint} did not become healthy within {timeout}s")


def start_server(args: argparse.Namespace) -> subprocess.Popen:
    if not args.model_path:
        raise SystemExit("--llama-server needs --model-path (the .gguf to load)")

    command = [
        args.llama_server,
        "-m",
        args.model_path,
        "-c",
        str(common.CONTEXT_TOKENS),
        "--threads",
        str(args.threads),
        "--n-gpu-layers",
        str(args.gpu_layers),
        "--port",
        args.endpoint.rsplit(":", 1)[-1],
    ]
    print("starting:", " ".join(command), flush=True)
    process = subprocess.Popen(command)

    try:
        wait_for_server(args.endpoint)
    except Exception:
        process.terminate()
        raise

    return process


def ask(endpoint: str, model: dict, case: dict, timeout: int) -> str:
    # The thinking hint already rides along in the user message, the way the app
    # appends it; no chat-template kwarg is involved.
    body = {
        "messages": common.build_messages(case, model),
        "temperature": common.TEMPERATURE,
        "top_p": common.TOP_P,
        "repeat_penalty": common.REPEAT_PENALTY,
        "max_tokens": common.max_new_tokens(case["input"]),
        "stream": False,
    }

    response = requests.post(f"{endpoint}/v1/chat/completions", json=body, timeout=timeout)
    response.raise_for_status()

    return response.json()["choices"][0]["message"]["content"]


def main() -> int:
    args = parse_args()
    model = catalog_entry(args.model)
    cases = common.load_cases()
    if args.limit:
        cases = cases[: args.limit]

    out_dir = common.raw_dir(args.model)
    out_dir.mkdir(parents=True, exist_ok=True)

    process = start_server(args) if args.llama_server else None
    rows: list[dict] = []

    try:
        for case in cases:
            started = time.time()
            try:
                output = ask(args.endpoint, model, case, args.timeout)
            except Exception as error:  # keep going: one bad case must not lose the run
                print(f"{case['id']}: FAILED ({error})", file=sys.stderr, flush=True)
                continue

            cleaned = clean(case["input"], output)
            failures = run_checks(case, cleaned)

            record = {
                "model": args.model,
                "case": case,
                "messages": common.build_messages(case, model),
                "raw_output": output,
                "cleaned_output": cleaned.text,
                "rejection": cleaned.rejection.value,
                "failed_checks": failures,
                "seconds": round(time.time() - started, 2),
            }
            (out_dir / f"{case['id']}.json").write_text(
                json.dumps(record, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
            )

            rows.append(
                {
                    "model": args.model,
                    "case_id": case["id"],
                    "action": case["action"],
                    "accepted": cleaned.accepted,
                    "rejection": cleaned.rejection.value,
                    "failed_checks": "|".join(failures),
                    "input_words": len(case["input"].split()),
                    "output_words": len(cleaned.text.split()),
                    "output": cleaned.text.replace("\n", "\\n"),
                }
            )
            verdict = "ok" if not failures else "FAIL:" + ",".join(failures)
            print(f"{case['id']}: {verdict} ({record['seconds']}s)", flush=True)
    finally:
        if process is not None:
            process.terminate()
            process.wait(timeout=30)

    if rows:
        write_summary(rows, args.model)
        failed = sum(1 for row in rows if row["failed_checks"])
        print(f"\n{len(rows)} cases, {failed} with a failed check. Summary: {common.RESULTS_DIR / 'summary.csv'}")

    return 0


def write_summary(rows: list[dict], model_id: str) -> None:
    """Append this run to summary.csv, replacing any earlier run of the model."""
    path = common.RESULTS_DIR / "summary.csv"
    existing: list[dict] = []

    if path.exists():
        with path.open(encoding="utf-8", newline="") as handle:
            existing = [row for row in csv.DictReader(handle) if row.get("model") != model_id]

    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=SUMMARY_FIELDS)
        writer.writeheader()
        writer.writerows(existing + rows)


if __name__ == "__main__":
    raise SystemExit(main())
