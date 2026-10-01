"""The same tests as ``tests/Wordwright.Core.Tests/OutputCleanerTests.cs``.

EVAL.md requires the Python port to carry the C# tests over: if the two
implementations ever disagree, the harness would score outputs the app would
have thrown away (or the reverse). Keep this file and the C# one in step.

Run:  python -m pytest eval/tests -q
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from cleaner import OutputRejection, clean  # noqa: E402

INPUT = "this is teh text"

# The tags are built from character codes rather than typed out, so no editor,
# linter or markup-rewriting tool can quietly eat them. The C# test helper does
# the same thing for the same reason.
_LT, _GT, _SLASH = chr(60), chr(62), chr(47)
_OPEN = _LT + "think" + _GT
_CLOSE = _LT + _SLASH + "think" + _GT


def think(content: str, close: bool) -> str:
    """A thinking tag, built rather than typed."""
    return _OPEN + content + (_CLOSE if close else "")


def test_clean_keeps_a_plain_rewrite_exactly():
    result = clean(INPUT, "This is the text.")

    assert result.accepted
    assert result.text == "This is the text."


# Rule 1
def test_clean_removes_a_thinking_block():
    result = clean(INPUT, think("The user wants grammar fixed.", close=True) + "This is the text.")

    assert result.text == "This is the text."


def test_clean_removes_an_unclosed_thinking_block_and_everything_after_it():
    output = "This is the text.\n" + think("and now I keep talking", close=False)

    result = clean(INPUT, output)

    assert result.text == "This is the text."


# Rule 2
@pytest.mark.parametrize(
    "output",
    [
        "Sure, here you go:\n\nThis is the text.",
        "Of course:\nThis is the text.",
        "Certainly! Here's the rewritten text:\nThis is the text.",
        "Rewritten:\nThis is the text.",
    ],
)
def test_clean_removes_leading_preambles(output):
    assert clean(INPUT, output).text == "This is the text."


# Rule 3
def test_clean_removes_one_pair_of_wrapping_quotes():
    assert clean(INPUT, '"This is the text."').text == "This is the text."


def test_clean_removes_smart_quotes_too():
    assert clean(INPUT, "“This is the text.”").text == "This is the text."


def test_clean_removes_a_wrapping_code_fence():
    assert clean(INPUT, "```\nThis is the text.\n```").text == "This is the text."


def test_clean_keeps_quotes_when_the_input_was_quoted():
    assert clean('"rough text"', '"This is the text."').text == '"This is the text."'


# Rule 4
@pytest.mark.parametrize(
    "output",
    [
        "This is the text.\n\nNote: I kept the meaning.",
        "This is the text.\nExplanation: grammar only.",
        "This is the text.\nI corrected the spelling.",
        'This is the text.\nChanges: fixed "teh".',
    ],
)
def test_clean_removes_trailing_notes(output):
    assert clean(INPUT, output).text == "This is the text."


def test_clean_keeps_a_note_that_is_not_at_the_end():
    output = "Note: this is part of the rewrite.\n\nAnd this continues."

    assert clean(INPUT, output).text == output


# Rule 5
def test_clean_keeps_the_inputs_trailing_newline():
    assert clean("line one\n", "line one rewritten\n").text == "line one rewritten\n"


def test_clean_trims_surrounding_whitespace():
    assert clean(INPUT, "\n\n  This is the text.  \n\n").text == "This is the text."


# Rule 6
def test_clean_rejects_an_empty_answer():
    result = clean(INPUT, "   \n  ")

    assert not result.accepted
    assert result.rejection is OutputRejection.EMPTY
    assert result.text == ""


def test_clean_rejects_output_that_ran_away():
    output = "x" * ((4 * len(INPUT)) + 201)

    result = clean(INPUT, output)

    assert not result.accepted
    assert result.rejection is OutputRejection.RUNAWAY


def test_clean_accepts_output_at_the_runaway_limit():
    output = "x" * ((4 * len(INPUT)) + 200)

    assert clean(INPUT, output).accepted
