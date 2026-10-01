"""Shared pieces for the evaluation harness.

Paths, the prompt wording and the generation limits are all copied from the app
(``src/Wordwright.Core/Actions/PromptBuilder.cs``, ``ActionSeeds.cs`` and
``docs/ARCHITECTURE.md``) so that what is measured here is what a user gets.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

EVAL_DIR = Path(__file__).resolve().parent
REPO_ROOT = EVAL_DIR.parent
CASES_DIR = EVAL_DIR / "cases"
RESULTS_DIR = EVAL_DIR / "results"
MODELS_JSON = REPO_ROOT / "models" / "models.json"

# docs/ARCHITECTURE.md -> Prompt
SYSTEM_MESSAGE = (
    "You rewrite text. Follow the instruction exactly.\n"
    "Reply with only the rewritten text: no introduction, no quotes, no notes, no explanation.\n"
    "Keep the original language unless the instruction says otherwise.\n"
    "Keep names, numbers, links and formatting such as line breaks and bullet points."
)

# docs/ARCHITECTURE.md -> Generation parameters
CONTEXT_TOKENS = 4096
MAX_INPUT_TOKENS = 1500
MAX_NEW_TOKENS_CAP = 1536
TEMPERATURE = 0.3
TOP_P = 0.9
REPEAT_PENALTY = 1.05

# docs/UX_COPY.md -> Built-in actions (mirrors ActionSeeds.cs). "custom" takes
# its instruction from the case file instead.
BUILT_IN_INSTRUCTIONS = {
    "fix": "Correct grammar, spelling and punctuation. "
    "Keep the meaning, tone and language. Change as little as possible.",
    "clear": "Rewrite so it is clear and easy to read. "
    "Keep the meaning and roughly the same length.",
    "formal": "Rewrite in a polite, professional tone suitable for work email. Keep the meaning.",
    "friendly": "Rewrite in a warm, friendly, natural tone. Keep the meaning.",
    "shorten": "Make it shorter and more direct. Keep every important point.",
}

ACTIONS = ["fix", "clear", "formal", "friendly", "shorten", "custom"]


def load_cases() -> list[dict]:
    """Every case in ``eval/cases/*.jsonl``, in file order."""
    cases: list[dict] = []

    for path in sorted(CASES_DIR.glob("*.jsonl")):
        with path.open(encoding="utf-8") as handle:
            for line_number, line in enumerate(handle, 1):
                line = line.strip()
                if not line:
                    continue
                try:
                    cases.append(json.loads(line))
                except json.JSONDecodeError as error:
                    raise ValueError(f"{path}:{line_number}: {error}") from error

    return cases


def load_catalog() -> dict:
    with MODELS_JSON.open(encoding="utf-8") as handle:
        return json.load(handle)


def catalog_models(status: str | None = "candidate") -> list[dict]:
    """Catalog entries, optionally filtered by ``status``."""
    models = load_catalog().get("models", [])
    if status is None:
        return models
    return [model for model in models if model.get("status") == status]


def instruction_for(case: dict) -> str:
    """The instruction the app would send for this case."""
    action = case["action"]

    if action == "custom":
        return case.get("instruction", "")

    if action not in BUILT_IN_INSTRUCTIONS:
        raise ValueError(f"unknown action {action!r} in case {case.get('id')!r}")

    return BUILT_IN_INSTRUCTIONS[action]


def build_messages(case: dict) -> list[dict]:
    """The exact two messages the app builds (PromptBuilder.Build)."""
    user = f"Instruction: {instruction_for(case)}\n\nText:\n{case['input']}"

    return [
        {"role": "system", "content": SYSTEM_MESSAGE},
        {"role": "user", "content": user},
    ]


def estimate_tokens(text: str) -> int:
    """A rough token count, for the max-new-tokens formula.

    The app counts tokens with the model's own tokenizer. The harness has to
    pick ``max_tokens`` before the request goes out, and llama-server applies
    the same chat template, so ~4 characters per token is close enough; the
    cap in ``max_new_tokens`` is what actually bounds a runaway generation.
    """
    return max(1, round(len(text) / 4))


def max_new_tokens(input_text: str) -> int:
    """``min(2 × input tokens + 64, 1,536)`` (PromptBuilder.MaxNewTokens)."""
    return min((2 * estimate_tokens(input_text)) + 64, MAX_NEW_TOKENS_CAP)


def disable_thinking(model: dict) -> bool:
    """Whether the catalog asks for "thinking" to be turned off.

    The catalog still carries TODO placeholders for the candidates, so only a
    real boolean/instruction counts.
    """
    hint = (model.get("promptHints") or {}).get("disableThinking")
    return isinstance(hint, bool) and hint


def raw_dir(model_id: str) -> Path:
    return RESULTS_DIR / "raw" / model_id


def api_key() -> str | None:
    return os.environ.get("JUDGE_API_KEY") or None
