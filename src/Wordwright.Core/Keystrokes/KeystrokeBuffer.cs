using System.Text;

namespace Wordwright.Core.Keystrokes;

/// <summary>
/// The rolling buffer of recently typed characters, kept in memory only
/// (docs/AGENTS.md rule 3). It holds at most the last
/// <see cref="MaxLength"/> characters, so a long typing session cannot grow it,
/// and it is cleared whenever the caret may have moved — including after a pause
/// longer than <see cref="TypingTimeout"/>, so a trigger typed with a long gap in
/// the middle of it never expands (decision D3, docs/PLAN.md → P2.7).
/// </summary>
public sealed class KeystrokeBuffer
{
    public const int MaxLength = 64;

    /// <summary>How long a pause between two typed characters may last before
    /// the earlier ones are forgotten. Not a setting in v1.</summary>
    public static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(5);

    private readonly StringBuilder _text = new(MaxLength);
    private readonly Func<DateTimeOffset> _clock;

    private DateTimeOffset? _lastAppendAt;

    /// <summary>Uses the system clock; the app's snippet engine does this.</summary>
    public KeystrokeBuffer()
        : this(() => DateTimeOffset.Now)
    {
    }

    /// <param name="clock">Supplies the current time, so tests can move it
    /// without waiting.</param>
    public KeystrokeBuffer(Func<DateTimeOffset> clock)
    {
        _clock = clock;
    }

    public string Text => _text.ToString();

    public int Length => _text.Length;

    /// <summary>Appends a typed character, dropping the oldest one when the
    /// buffer is already full. A pause longer than <see cref="TypingTimeout"/>
    /// since the previous character starts the buffer over.</summary>
    public void Append(char character)
    {
        var now = _clock();

        if (_lastAppendAt is { } previous && now - previous > TypingTimeout)
        {
            _text.Clear();
        }

        _lastAppendAt = now;

        if (_text.Length == MaxLength)
        {
            _text.Remove(0, 1);
        }

        _text.Append(character);
    }

    /// <summary>Removes the last character; does nothing on an empty buffer.</summary>
    public void Backspace()
    {
        if (_text.Length > 0)
        {
            _text.Length--;
        }
    }

    /// <summary>Drops the last <paramref name="count"/> characters, which is what
    /// the screen shows after a trigger's characters have been deleted.</summary>
    public void RemoveLast(int count)
    {
        if (count > 0 && _text.Length > 0)
        {
            _text.Length = Math.Max(0, _text.Length - count);
        }
    }

    /// <summary>Forgets everything, e.g. after Enter, a click, or an expansion.</summary>
    public void Clear()
    {
        _text.Clear();
        _lastAppendAt = null;
    }
}