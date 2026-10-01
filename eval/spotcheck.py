"""Human spot check of the judge (docs/EVAL.md → Human spot check).

Shows a random 10% of the outputs, one at a time and blind — the model that
produced each one is not shown — and asks for the same four scores the judge
gave. It then reports how closely the judge agrees with you. If agreement is
below 70%, the rubric needs fixing before any score here should be trusted.

    python eval/spotcheck.py
"""

from __future__ import annotations

import argparse
import json
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import common
from common import instruction_for
from judge import RUBRIC, load_outputs

AGREEMENT_TARGET = 0.70
AGREEMENT_TOLERANCE = 1


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--share", type=float, default=0.1, help="fraction of outputs to check (default 10%%)")
    parser.add_argument("--seed", type=int, default=4242)
    return parser.parse_args()


def ask_for_scores() -> dict:
    scores = {}

    for criterion in RUBRIC:
        while True:
            raw = input(f"  {criterion.replace('_', ' ')} (1-5): ").strip()
            if raw.isdigit() and 1 <= int(raw) <= 5:
                scores[criterion] = int(raw)
                break
            print("  please type a whole number from 1 to 5")

    return scores


def main() -> int:
    args = parse_args()

    scores_path = common.RESULTS_DIR / "scores.json"
    if not scores_path.exists():
        raise SystemExit("run eval/judge.py first: scores.json is needed for the comparison")

    judge_scores = json.loads(scores_path.read_text(encoding="utf-8"))
    items = load_outputs()
    if not items:
        raise SystemExit("no raw outputs found; run eval/run_candidates.py first")

    random.Random(args.seed).shuffle(items)
    chosen = items[: max(1, round(len(items) * args.share))]

    print(f"{len(items)} outputs; checking {len(chosen)}.\n")

    results = []
    for index, item in enumerate(chosen, 1):
        case = item["case"]
        print("=" * 72)
        print(f"[{index}/{len(chosen)}] action: {case['action']}")
        print(f"\nInstruction: {instruction_for(case)}")
        print(f"\nOriginal:\n{case['input']}")
        print(f"\nRewrite:\n{item['output']}\n")

        human = ask_for_scores()
        record = {
            "model": item["model"],
            "case_id": case["id"],
            "human": human,
            "judge": judge_scores["models"].get(item["model"], {}).get("cases", {}).get(case["id"], {}).get("judges"),
        }
        results.append(record)
        print()

    write_report(results)

    return 0


def write_report(results: list[dict]) -> None:
    compared = [row for row in results if row["judge"]]
    per_criterion: dict[str, list[bool]] = {criterion: [] for criterion in RUBRIC}
    per_case: list[bool] = []

    for row in compared:
        within = []
        for criterion in RUBRIC:
            judge_mean = sum(scores[criterion] for scores in row["judge"]) / len(row["judge"])
            agrees = abs(row["human"][criterion] - judge_mean) <= AGREEMENT_TOLERANCE
            per_criterion[criterion].append(agrees)
            within.append(agrees)
        per_case.append(all(within))

    case_agreement = sum(per_case) / len(per_case) if per_case else 0.0

    out = {
        "checked": len(results),
        "compared": len(compared),
        "tolerance": AGREEMENT_TOLERANCE,
        "caseAgreement": round(case_agreement, 3),
        "criterionAgreement": {
            criterion: round(sum(values) / len(values), 3) if values else 0.0
            for criterion, values in per_criterion.items()
        },
        "target": AGREEMENT_TARGET,
        "results": results,
    }

    path = common.RESULTS_DIR / "spotcheck.json"
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print("=" * 72)
    print(f"human-judge agreement: {case_agreement:.0%} of cases within ±{AGREEMENT_TOLERANCE}")
    for criterion, values in per_criterion.items():
        share = sum(values) / len(values) if values else 0.0
        print(f"  {criterion.replace('_', ' '):22} {share:.0%}")
    print(f"\nwrote {path}")

    if compared and case_agreement < AGREEMENT_TARGET:
        print(f"\nAGREEMENT IS BELOW {AGREEMENT_TARGET:.0%}: fix the rubric before trusting the scores.")


if __name__ == "__main__":
    raise SystemExit(main())
