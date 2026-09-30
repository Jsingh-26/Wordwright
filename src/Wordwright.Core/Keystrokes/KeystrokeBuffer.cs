using System.Text;

namespace Wordwright.Core.Keystrokes;

/// <summary>
/// The rolling buffer of recently typed characters, kept in memory only
/// (docs/AGENTS.md rule 3). It holds at most the last
/// <see cref="MaxLength"/> characters, so a long typing session cannot grow it,
/// and it is cleared whenever the caret may have moved.
/// </summary>
public sealed class KeystrokeBuffer
{
    public const int MaxLength = 64;

    private readonly StringBuilder _text = new(MaxLength);

    public string Text => _text.ToString();

    public int Length => _text.Length;

    /// <summary>Appends a typed character, dropping the oldest one when the
    /// buffer is already full.</summary>
    public void Append(char character)
    {
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
    }
}