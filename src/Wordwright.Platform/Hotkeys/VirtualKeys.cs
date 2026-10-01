using Wordwright.Core.Actions;

namespace Wordwright.Platform.Hotkeys;

/// <summary>
/// Turns a <see cref="HotkeySpec"/>'s names into the Win32 virtual-key codes
/// <c>RegisterHotKey</c> wants. The parsing rules live in Core; only the mapping
/// to Windows lives here.
/// </summary>
internal static class VirtualKeys
{
    /// <summary>The virtual-key code for a spec's key, or 0 when it is unknown.</summary>
    internal static uint For(string key)
    {
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            return char.ToUpperInvariant(key[0]);
        }

        if (key.Length is >= 2 and <= 3 && (key[0] is 'f' or 'F')
            && int.TryParse(key[1..], out var number) && number is >= 1 and <= HotkeySpec.MaxFunctionKey)
        {
            return 0x70u + (uint)number - 1;
        }

        return Named.TryGetValue(key, out var code) ? code : 0;
    }

    /// <summary>The <c>HotkeyModifiers</c> a virtual key stands for, or
    /// <see cref="HotkeyModifiers.None"/> when it is not a modifier.</summary>
    internal static HotkeyModifiers ModifierFor(uint virtualKey) => virtualKey switch
    {
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,     // Shift, LShift, RShift
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,   // Control, LControl, RControl
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,       // Menu, LMenu, RMenu
        0x5B or 0x5C => HotkeyModifiers.Win,               // LWin, RWin
        _ => HotkeyModifiers.None,
    };

    /// <summary>The <c>MOD_*</c> flags <c>RegisterHotKey</c> takes.</summary>
    internal static uint Modifiers(HotkeyModifiers modifiers)
    {
        var flags = 0u;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            flags |= 0x0001; // MOD_ALT
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            flags |= 0x0002; // MOD_CONTROL
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            flags |= 0x0004; // MOD_SHIFT
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            flags |= 0x0008; // MOD_WIN
        }

        return flags | 0x4000; // MOD_NOREPEAT: one message per physical press.
    }

    private static readonly Dictionary<string, uint> Named = new(StringComparer.Ordinal)
    {
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Escape"] = 0x1B,
        ["Enter"] = 0x0D,
        ["Backspace"] = 0x08,
        ["Delete"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
    };
}
