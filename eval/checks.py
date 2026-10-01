"""Automatic checks applied to every output (docs/EVAL.md → Automatic checks).

A failed check caps that case's score at 2, so a rewrite that loses a name or
leaks a preamble can never look good just because it reads nicely.
"""

from __future__ import annotations

from cleaner import CleanedOutput, leftover_preamble_or_note

__all__ = ["SCORE_CAP", "run_checks", "apply_cap"]

# docs/EVAL.md: "A failed check caps that case's score at 2."
SCORE_CAP = 2.0

SHORTEN_MAX_RATIO = 0.85
OTHER_MIN_RATIO = 0.6
OTHER_MAX_RATIO = 1.6


def run_checks(case: dict, cleaned: CleanedOutput) -> list[str]:
    """Names of the checks this output failed (empty when it passed all)."""
    if not cleaned.accepted:
        # Nothing to measure on a rejected answer; the cleaner already said no.
        return ["empty_or_runaway"]

    failures: list[str] = []
    text = cleaned.text

    if leftover_preamble_or_note(text) is not None:
        failures.append("preamble_left")

    if any(kept not in text for kept in case.get("must_keep", [])):
        failures.append("must_keep_missing")

    ratio = length_ratio(case["input"], text)

    if case["action"] == "shorten":
        if ratio > SHORTEN_MAX_RATIO:
            failures.append("length_ratio")
    elif not OTHER_MIN_RATIO <= ratio <= OTHER_MAX_RATIO:
        # Note: this range is what docs/EVAL.md asks for on every action other
        # than "shorten", and it includes "custom". A custom instruction such as
        # "Summarise this in three sentences" is legitimately far shorter than
        # its input, so those cases can trip this check by design of the case
        # rather than by a fault in the model. Left as documented; change the
        # line above if custom instructions should be exempt.
        failures.append("length_ratio")

    return failures


def length_ratio(input_text: str, output_text: str) -> float:
    """Output length ÷ input length, in words."""
    input_words = len(input_text.split())

    if input_words == 0:
        return 0.0

    return len(output_text.split()) / input_words


def apply_cap(score: float, failures: list[str]) -> float:
    """The judged score, capped when any automatic check failed."""
    return min(score, SCORE_CAP) if failures else score
