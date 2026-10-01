"""Score candidate outputs with judge models (docs/EVAL.md → Judge).

Two strong models score every output blind — the judge never learns which model
produced the text and the order is shuffled — and their scores are averaged to
reduce single-judge bias. Automatic-check failures cap a case's score at 2
(``eval/checks.py``).

Configuration is by environment variable:

    JUDGE_BASE_URL   e.g. https://ollama.com/v1
    JUDGE_API_KEY    the key for that endpoint
    JUDGE_MODELS     comma-separated, e.g. kimi-k3,glm-5.3

Run after ``run_candidates.py``:

    python eval/judge.py
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import sys
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))

import common
from checks import apply_cap
from common import instruction_for

RUBRIC = ["meaning_kept", "instruction_followed", "correct_and_natural", "clean_output"]

JUDGE_SYSTEM = (
    "You are a strict evaluator of text rewrites. "
    "You reply with a single JSON object and nothing else."
)

JUDGE_TEMPLATE = """The task given to a rewriting model was:

Instruction: {instruction}

Original text:
{input}

The model produced:

{output}

Score that output from 1 (bad) to 5 (excellent) on each of:
- meaning_kept: the original meaning survives.
- instruction_followed: the instruction was actually carried out.
- correct_and_natural: the result is correct and reads naturally.
- clean_output: the output is only the rewritten text, with no preamble, quotes, notes or explanation.

Reply with exactly this JSON and nothing else:
{{"meaning_kept": 0, "instruction_followed": 0, "correct_and_natural": 0, "clean_output": 0}}"""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--base-url", default=os.environ.get("JUDGE_BASE_URL", "https://ollama.com/v1"))
    parser.add_argument("--models", default=os.environ.get("JUDGE_MODELS", ""), help="comma-separated judge models")
    parser.add_argument("--seed", type=int, default=20261001, help="shuffle seed, so a rerun scores the same order")
    parser.add_argument("--timeout", type=int, default=180)
    return parser.parse_args()


def load_outputs() -> list[dict]:
    """Every raw result on disk, across all models."""
    items: list[dict] = []
    raw = common.RESULTS_DIR / "raw"

    if not raw.exists():
        return items

    for path in sorted(raw.glob("*/*.json")):
        record = json.loads(path.read_text(encoding="utf-8"))
        items.append(
            {
                "model": record["model"],
                "case": record["case"],
                "output": record["cleaned_output"] or record["raw_output"],
                "failed_checks": record.get("failed_checks", []),
            }
        )

    return items


def extract_json(text: str) -> dict | None:
    """The first JSON object in ``text``, or None when there is not one."""
    start = text.find("{")

    while start != -1:
        depth = 0
        for index in range(start, len(text)):
            if text[index] == "{":
                depth += 1
            elif text[index] == "}":
                depth -= 1
                if depth == 0:
                    try:
                        candidate = json.loads(text[start : index + 1])
                    except json.JSONDecodeError:
                        break
                    if all(key in candidate for key in RUBRIC):
                        return candidate
                    break
        start = text.find("{", start + 1)

    return None


def cache_path(judge: str, item: dict) -> Path:
    key = hashlib.sha256(f"{item['model']}\x00{item['case']['id']}\x00{item['output']}".encode()).hexdigest()[:16]
    safe_judge = judge.replace("/", "_")
    return common.RESULTS_DIR / "judged" / safe_judge / f"{key}.json"


def ask_judge(base_url: str, api_key: str | None, judge: str, item: dict, timeout: int) -> dict:
    """One judge's scores for one output, cached on disk."""
    cached = cache_path(judge, item)
    if cached.exists():
        return json.loads(cached.read_text(encoding="utf-8"))

    prompt = JUDGE_TEMPLATE.format(
        instruction=instruction_for(item["case"]),
        input=item["case"]["input"],
        output=item["output"],
    )
    body = {
        "model": judge,
        "temperature": 0,
        "messages": [
            {"role": "system", "content": JUDGE_SYSTEM},
            {"role": "user", "content": prompt},
        ],
    }
    headers = {"Authorization": f"Bearer {api_key}"} if api_key else {}

    scores = None
    for _ in range(2):  # retry once on invalid JSON
        response = requests.post(f"{base_url}/chat/completions", json=body, headers=headers, timeout=timeout)
        response.raise_for_status()
        scores = extract_json(response.json()["choices"][0]["message"]["content"])
        if scores is not None:
            break

    if scores is None:
        raise ValueError(f"{judge} did not return the rubric JSON for {item['case']['id']}")

    cached.parent.mkdir(parents=True, exist_ok=True)
    cached.write_text(json.dumps(scores, indent=2) + "\n", encoding="utf-8")

    return scores


def main() -> int:
    args = parse_args()
    judges = [model.strip() for model in args.models.split(",") if model.strip()]

    if not judges:
        raise SystemExit("set JUDGE_MODELS (or --models) to one or more judge model names")

    items = load_outputs()
    if not items:
        raise SystemExit("no raw outputs found; run eval/run_candidates.py first")

    random.Random(args.seed).shuffle(items)  # blind order, stable across reruns
    print(f"{len(items)} outputs, {len(judges)} judges: {', '.join(judges)}")

    per_model: dict[str, dict] = {}

    for index, item in enumerate(items, 1):
        model = item["model"]
        case_id = item["case"]["id"]
        action = item["case"]["action"]

        judge_scores = [ask_judge(args.base_url, common.api_key(), judge, item, args.timeout) for judge in judges]
        mean = sum(sum(scores[key] for key in RUBRIC) / len(RUBRIC) for scores in judge_scores) / len(judges)
        capped = apply_cap(mean, item["failed_checks"])

        bucket = per_model.setdefault(model, {"cases": {}, "actions": {}})
        bucket["cases"][case_id] = {
            "action": action,
            "judges": judge_scores,
            "mean": round(mean, 3),
            "score": round(capped, 3),
            "failed_checks": item["failed_checks"],
        }
        bucket["actions"].setdefault(action, []).append(capped)

        print(f"[{index}/{len(items)}] {model} {case_id}: {capped:.2f}", flush=True)

    write_scores(per_model, judges)

    return 0


def write_scores(per_model: dict[str, dict], judges: list[str]) -> None:
    out = {"judges": judges, "models": {}}

    for model, bucket in sorted(per_model.items()):
        actions = {action: round(sum(values) / len(values), 3) for action, values in sorted(bucket["actions"].items())}
        overall = round(sum(actions.values()) / len(actions), 3) if actions else 0.0
        out["models"][model] = {
            "evalScores": {**actions, "overall": overall},
            "cases": bucket["cases"],
            "cappedCases": sum(1 for case in bucket["cases"].values() if case["failed_checks"]),
        }

    path = common.RESULTS_DIR / "scores.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"\nwrote {path}")


if __name__ == "__main__":
    raise SystemExit(main())
