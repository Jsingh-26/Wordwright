using System.Runtime.InteropServices;

namespace Wordwright.Platform.Input;

/// <summary>
/// Sends the keystrokes Wordwright needs: Backspaces to remove the trigger the
/// user typed, Ctrl+V to paste, and Left arrows to put the caret back where a
/// body's <c>{cursor}</c> marker was. Everything it sends is injected, and the
/// keyboard hook ignores injected input, so this never feeds itself.
/// </summary>
public static class InputSender
{
    private const ushort VK_BACK = 0x08;
    private const ushort VK_C = 0x43;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_V = 0x56;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>Enough per call to be quick, small enough to stay well inside
    /// what SendInput accepts.</summary>
    private const int KeysPerBatch = 64;

    public static void SendBackspaces(int count) => Tap(VK_BACK, count);

    public static void SendLeftArrows(int count) => Tap(VK_LEFT, count);

    /// <summary>Presses and releases Ctrl+C, for capturing the selection.</summary>
    public static void Copy() => Combo(VK_C);

    /// <summary>Presses and releases Ctrl+V.</summary>
    public static void Paste() => Combo(VK_V);

    private static void Combo(ushort virtualKey)
    {
        var inputs = new[]
        {
            Key(VK_CONTROL, down: true),
            Key(virtualKey, down: true),
            Key(virtualKey, down: false),
            Key(VK_CONTROL, down: false),
        };

        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void Tap(ushort virtualKey, int count)
    {
        while (count > 0)
        {
            var batch = Math.Min(KeysPerBatch, count);
            var inputs = new Input[batch * 2];

            for (var index = 0; index < batch; index++)
            {
                inputs[index * 2] = Key(virtualKey, down: true);
                inputs[(index * 2) + 1] = Key(virtualKey, down: false);
            }

            _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            count -= batch;
        }
    }

    private static Input Key(ushort virtualKey, bool down) => new()
    {
        Type = INPUT_KEYBOARD,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = 0,
                Flags = down ? 0 : KEYEVENTF_KEYUP,
            },
        },
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}