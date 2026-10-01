"""Python port of Wordwright's ``OutputCleaner``.

Source of truth: ``src/Wordwright.Core/Actions/OutputCleaner.cs`` and the rules
in ``docs/ARCHITECTURE.md`` ("OutputCleaner rules"). The harness must clean a
model's answer with the *same* rules the app uses, otherwise a score here would
not mean the same thing as a paste in Notepad. ``eval/tests/test_cleaner.py``
mirrors ``tests/Wordwright.Core.Tests/OutputCleanerTests.cs`` rule for rule, so
the two implementations stay in step.

Keep this file boring: it is a translation, not an improvement.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from enum import Enum

__all__ = ["CleanedOutput", "OutputRejection", "clean", "leftover_preamble_or_note"]


class OutputRejection(Enum):
    """Why the model's answer was not usable as a rewrite."""

    NONE = "none"
    """It cleaned up to something worth pasting."""

    EMPTY = "empty"
    """Nothing was left once the scaffolding was removed."""

    RUNAWAY = "runaway"
    """Far longer than the input, so the model ran away (rule 6)."""


@dataclass(frozen=True)
class CleanedOutput:
    """The clean text, or the reason it was rejected."""

    text: str
    rejection: OutputRejection

    @property
    def accepted(self) -> bool:
        return self.rejection is OutputRejection.NONE


# Rule 1: a <think>...</think> block, and a thinking block the model opened and
# never closed (in which case everything after it goes too).
_THINKING_BLOCK = re.compile(r"<think\b[^>]*>.*?</think>", re.S | re.I)
_UNCLOSED_THINKING = re.compile(r"<think\b[^>]*>.*$", re.S | re.I)

# Rule 2: a line that only announces the rewrite.
_PREAMBLE = re.compile(
    r"^(sure|okay|of course|certainly|here('s| is| are)|rewritten|corrected|revised)\b.*:?\s*$",
    re.I,
)

# Rule 4: lines a model adds to explain itself.
_NOTE_PREFIXES = (
    "Note:",
    "Explanation:",
    "(Note",
    "I changed",
    "I made",
    "I corrected",
    "Changes:",
)

# Rule 6: a rewrite may not be longer than this, or it ran away.
_RUNAWAY_FACTOR = 4
_RUNAWAY_SLACK = 200


def clean(input_text: str, output: str) -> CleanedOutput:
    """Clean ``output``, written in reply to ``input_text``."""
    if input_text is None or output is None:
        raise ValueError("input and output are required")

    text = _THINKING_BLOCK.sub("", output)
    text = _UNCLOSED_THINKING.sub("", text)

    text = _remove_preamble(text)
    text = _remove_wrapping(text, _input_is_wrapped(input_text))
    text = _remove_trailing_notes(text)

    text = text.strip()

    if len(text) == 0:
        return CleanedOutput("", OutputRejection.EMPTY)

    if _length(text) > (_RUNAWAY_FACTOR * _length(input_text)) + _RUNAWAY_SLACK:
        return CleanedOutput("", OutputRejection.RUNAWAY)

    # Rule 5: a body that ended in a newline keeps one, so a snippet the user
    # was editing does not lose its last line break.
    if input_text.endswith("\n"):
        text += "\n"

    return CleanedOutput(text, OutputRejection.NONE)


def _remove_preamble(text: str) -> str:
    """Rule 2: drop the lines at the top that only say "here you go"."""
    lines = _split(text)
    start = 0

    while start < len(lines) and _PREAMBLE.match(lines[start]):
        start += 1

    return text if start == 0 else _join(lines, start, len(lines))


def _remove_wrapping(text: str, input_is_wrapped: bool) -> str:
    """Rule 3: strip one pair of wrapping quotes or one wrapping code fence.

    Only when the input was not wrapped that way itself — a rewrite of already
    quoted text should keep its quotes.
    """
    if input_is_wrapped:
        return text

    trimmed = text.strip()

    if trimmed.startswith("```"):
        open_end = trimmed.find("\n")
        close_start = trimmed.rfind("```")

        if open_end >= 0 and close_start > open_end:
            return trimmed[open_end + 1 : close_start]

    if len(trimmed) >= 2 and _is_closing_quote(trimmed[0], trimmed[-1]):
        return trimmed[1:-1]

    return text


def _remove_trailing_notes(text: str) -> str:
    """Rule 4: cut a note the model tacked on at the end."""
    lines = _split(text)

    for index in range(len(lines)):
        if not _starts_note(lines[index]):
            continue

        # Only a note that runs to the end counts: a "Note:" followed by more
        # rewritten text is part of the rewrite.
        if _has_paragraph_after(lines, index):
            continue

        return _join(lines, 0, index)

    return text


def _has_paragraph_after(lines: list[str], index: int) -> bool:
    for nxt in range(index + 1, len(lines)):
        if lines[nxt].strip() == "" and nxt + 1 < len(lines) and lines[nxt + 1].strip() != "":
            return True

    return False


def _join(lines: list[str], start: int, end: int) -> str:
    parts: list[str] = []

    for index in range(start, end):
        if index > start:
            parts.append("\n")

        parts.append(lines[index])

    # Trailing blank lines left behind by a removed note go with it.
    return "".join(parts).rstrip("\r\n \t")


def _split(text: str) -> list[str]:
    return text.replace("\r\n", "\n").split("\n")


def _starts_note(line: str) -> bool:
    trimmed = line.lstrip()
    lowered = trimmed.lower()

    return any(lowered.startswith(prefix.lower()) for prefix in _NOTE_PREFIXES)


def _input_is_wrapped(input_text: str) -> bool:
    trimmed = input_text.strip()

    return trimmed.startswith("```") or (len(trimmed) > 0 and _is_open_quote(trimmed[0]))


def _is_open_quote(character: str) -> bool:
    return character in ('"', "'", "“", "‘")


def _is_closing_quote(open_quote: str, close_quote: str) -> bool:
    if open_quote in ('"', "“"):
        return close_quote in ('"', "”")

    if open_quote in ("'", "‘"):
        return close_quote in ("'", "’")

    return False


def leftover_preamble_or_note(text: str) -> str | None:
    """The first line that still looks like a preamble or a trailing note.

    Used by ``eval/checks.py`` for the ``preamble_left`` check: after cleaning,
    a line that still announces the rewrite (or explains it) means the model
    phrased its scaffolding in a way the cleaner's rules did not catch.
    Returns ``None`` when the text is clean.
    """
    for line in _split(text):
        if _PREAMBLE.match(line) or _starts_note(line):
            return line

    return None


def _length(text: str) -> int:
    """``System.String.Length`` counts UTF-16 code units, not code points."""
    return sum(2 if ord(character) > 0xFFFF else 1 for character in text)
