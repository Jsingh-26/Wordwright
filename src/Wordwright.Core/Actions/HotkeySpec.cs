using System.Text;

namespace Wordwright.Core.Actions;

/// <summary>The modifier keys a hotkey can use.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A hotkey as it is stored and shown, e.g. <c>Ctrl+Alt+G</c>
/// (docs/ARCHITECTURE.md → Hotkeys). Parsing and validation are here, in Core,
/// so they are unit-testable; the Win32 registration is in
/// <c>Wordwright.Platform</c>.
/// </summary>
public sealed record HotkeySpec
{
    /// <summary>Function keys beyond this are not accepted.</summary>
    public const int MaxFunctionKey = 24;

    public HotkeyModifiers Modifiers { get; init; }

    /// <summary>The non-modifier key: a letter, a digit, <c>Space</c>, or
    /// <c>F1</c>–<c>F24</c>.</summary>
    public string Key { get; init; } = "";

    /// <summary>True when at least one modifier and one usable key are set.</summary>
    public bool IsUsable => Modifiers != HotkeyModifiers.None && IsKnownKey(Key);

    /// <summary>Parses <c>Ctrl+Alt+G</c> and the like. Returns null when the text
    /// is not a hotkey.</summary>
    public static HotkeySpec? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var modifiers = HotkeyModifiers.None;

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (Modifier(parts[index]) is not { } modifier || modifiers.HasFlag(modifier))
            {
                // An unknown name, or the same modifier twice, is not a hotkey.
                return null;
            }

            modifiers |= modifier;
        }

        var key = NormalizeKey(parts[^1]);
        if (key is null || modifiers == HotkeyModifiers.None)
        {
            return null;
        }

        return new HotkeySpec { Modifiers = modifiers, Key = key };
    }

    /// <summary>The written form, in a fixed modifier order.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();

        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            builder.Append("Ctrl+");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            builder.Append("Alt+");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            builder.Append("Shift+");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            builder.Append("Win+");
        }

        return builder.Append(Key).ToString();
    }

    private static HotkeyModifiers? Modifier(string name) => name.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" => HotkeyModifiers.Win,
        _ => null,
    };

    private static string? NormalizeKey(string key)
    {
        if (key.Length == 1)
        {
            var character = char.ToUpperInvariant(key[0]);
            return char.IsAsciiLetterOrDigit(character) ? character.ToString() : null;
        }

        if (string.Equals(key, "Space", StringComparison.OrdinalIgnoreCase))
        {
            return "Space";
        }

        if (key.Length is >= 2 and <= 3
            && (key[0] is 'f' or 'F')
            && int.TryParse(key[1..], out var number)
            && number is >= 1 and <= MaxFunctionKey)
        {
            return "F" + number;
        }

        return NamedKeys.TryGetValue(key, out var named) ? named : null;
    }

    /// <summary>The named keys a hotkey may use, normalised to one spelling.</summary>
    private static readonly Dictionary<string, string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = "Tab",
        ["Esc"] = "Escape",
        ["Escape"] = "Escape",
        ["Enter"] = "Enter",
        ["Return"] = "Enter",
        ["Backspace"] = "Backspace",
        ["Delete"] = "Delete",
        ["Del"] = "Delete",
        ["Insert"] = "Insert",
        ["Home"] = "Home",
        ["End"] = "End",
        ["PageUp"] = "PageUp",
        ["PageDown"] = "PageDown",
        ["Up"] = "Up",
        ["Down"] = "Down",
        ["Left"] = "Left",
        ["Right"] = "Right",
    };

    private static bool IsKnownKey(string key) => NormalizeKey(key) is not null;
}

/// <summary>How a hotkey passed validation.</summary>
public enum HotkeyValidation
{
    Ok,

    /// <summary>No modifier, no key, or an unknown key.</summary>
    Invalid,

    /// <summary>A combination Windows keeps for itself.</summary>
    Reserved,
}

/// <summary>
/// The hotkey rules from docs/ARCHITECTURE.md → Hotkeys: a modifier plus a key,
/// nothing Windows already owns, and no two actions sharing one.
/// </summary>
public static class HotkeyRules
{
    /// <summary>Combinations Windows reserves, which are reported as Reserved
    /// rather than "in use by another app".</summary>
    private static readonly HotkeySpec[] Reserved =
    [
        new() { Modifiers = HotkeyModifiers.Win, Key = "L" },
        new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = "Delete" },
        new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift, Key = "Escape" },
        new() { Modifiers = HotkeyModifiers.Alt, Key = "Tab" },
        new() { Modifiers = HotkeyModifiers.Alt, Key = "Escape" },
        new() { Modifiers = HotkeyModifiers.Alt, Key = "F4" },
    ];

    public static HotkeyValidation Validate(string? text)
    {
        if (HotkeySpec.Parse(text) is not { } spec || !spec.IsUsable)
        {
            return HotkeyValidation.Invalid;
        }

        return IsReserved(spec) ? HotkeyValidation.Reserved : HotkeyValidation.Ok;
    }

    /// <summary>Whether Windows keeps this combination for itself.</summary>
    public static bool IsReserved(HotkeySpec spec)
    {
        // "Delete" and "Escape" are not otherwise valid keys, so they are parsed
        // here the same way the reserved list holds them.
        return Reserved.Any(reserved =>
            reserved.Modifiers == spec.Modifiers
            && string.Equals(reserved.Key, spec.Key, StringComparison.Ordinal));
    }

    /// <summary>Whether two actions want the same hotkey. The palette hotkey and
    /// action hotkeys share one space, so the caller passes every one of them.</summary>
    public static bool IsDuplicate(
        IEnumerable<HotkeySpec> existing,
        HotkeySpec candidate,
        HotkeySpec? except = null)
    {
        return existing.Any(other =>
            !ReferenceEquals(other, except)
            && other.Modifiers == candidate.Modifiers
            && string.Equals(other.Key, candidate.Key, StringComparison.Ordinal));
    }
}
