using System.Runtime.InteropServices;
using System.Windows.Forms;
using Wordwright.Platform.Keyboard;

// Probe 1: does Shift / Caps Lock / AltGr (German layout) reach the hook's ToUnicodeEx translation?
// Probe 2: does the hook survive a garbage collection? (Is the hook delegate rooted?)
// Printable keys are only injected when our own form owns the foreground, so
// nothing is typed into another window. The GC probe uses a bare Ctrl tap,
// which types nothing anywhere.

internal static class Program
{
[STAThread]
static void Main(string[] args)
{
    var gcOnly = args.Contains("--gc-only");
    var wasLoaded = GetLayouts().Any(h => ((long)h & 0xFFFF) == 0x0407);
    var received = new List<char>();
    var clears = 0;
    using var hook = new KeyboardHook([], ignoreInjectedInput: false);
    hook.CharacterTyped += (_, c) => { lock (received) received.Add(c); };
    hook.BufferCleared += (_, _) => Interlocked.Increment(ref clears);
    hook.Start();
    Console.WriteLine($"installed={hook.IsInstalled}");

    if (!gcOnly)
    {
        using var form = new Form { Text = "HookProbe", Width = 300, Height = 120, TopMost = true };
        var box = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(box);
        form.Show();
        form.Activate();
        SetForegroundWindow(form.Handle);
        Pump(400);
        var fg = GetForegroundWindow();
        if (fg != form.Handle)
        {
            Console.WriteLine("could not take foreground; skipping printable-key probes");
        }
        else
        {
            // Shift+S
            received.Clear();
            Key(0xA0, down: true); Pump(30);
            Key(0x53, down: true); Key(0x53, down: false); Pump(30);
            Key(0xA0, down: false); Pump(500);
            Console.WriteLine($"Shift+S -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

            // plain s
            received.Clear(); box.Clear();
            Key(0x53, down: true); Key(0x53, down: false); Pump(500);
            Console.WriteLine($"s       -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

            // Shift+1 (= ! on US layout)
            received.Clear(); box.Clear();
            Key(0xA0, down: true); Pump(30);
            Key(0x31, down: true); Key(0x31, down: false); Pump(30);
            Key(0xA0, down: false); Pump(500);
            Console.WriteLine($"Shift+1 -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

            // Caps Lock on, s, Caps Lock off
            received.Clear(); box.Clear();
            Key(0x14, down: true); Key(0x14, down: false); Pump(100);
            Key(0x53, down: true); Key(0x53, down: false); Pump(100);
            Key(0x14, down: true); Key(0x14, down: false); Pump(500);
            Console.WriteLine($"Caps+s  -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

            // German layout, for this thread only and unloaded afterwards:
            // ; is Shift+, and @ is AltGr+Q (AltGr arrives as LCtrl + RAlt).
            var german = LoadKeyboardLayout("00000407", 0);
            if (german == IntPtr.Zero)
            {
                Console.WriteLine("German layout unavailable; skipping");
            }
            else
            {
                var previous = ActivateKeyboardLayout(german, 0);
                Pump(200);

                received.Clear(); box.Clear();
                Key(0xA0, down: true); Pump(30);
                Key(0xBC, down: true); Key(0xBC, down: false); Pump(30);
                Key(0xA0, down: false); Pump(500);
                Console.WriteLine($"DE Shift+, -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

                received.Clear(); box.Clear();
                Key(0xA2, down: true); Key(0xA5, down: true, extended: true); Pump(30);
                Key(0x51, down: true); Key(0x51, down: false); Pump(30);
                Key(0xA5, down: false, extended: true); Key(0xA2, down: false); Pump(500);
                Console.WriteLine($"DE AltGr+Q -> hook saw [{string.Concat(received)}], textbox has [{box.Text}]");

                ActivateKeyboardLayout(previous, 0);
                if (!wasLoaded) _ = UnloadKeyboardLayout(german);
            }
        }
        form.Close();
    }

    // GC probe: collect everything unreachable, then make the hook fire once.
    Console.WriteLine("forcing GC…");
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    Pump(200);
    var before = clears;
    Console.WriteLine("tapping Ctrl (types nothing) so the hook callback runs…");
    Console.Out.Flush();
    Key(0x11, down: true); Key(0x11, down: false);
    Pump(800);
    Console.WriteLine($"survived GC; hook still installed={hook.IsInstalled}; clears delta={clears - before}");
    hook.Stop();
}

static void Pump(int ms) { var end = Environment.TickCount64 + ms; while (Environment.TickCount64 < end) { Application.DoEvents(); Thread.Sleep(10); } }

static void Key(ushort vk, bool down, bool extended = false)
{
    var scan = (ushort)MapVirtualKey(vk, 0);
    var flags = (down ? 0u : 2u) | (extended ? 1u : 0u);
    var input = new INPUT { Type = 1, U = new U { K = new KEYBDINPUT { Vk = vk, Scan = scan, Flags = flags } } };
    _ = SendInput(1, [input], Marshal.SizeOf<INPUT>());
    Thread.Sleep(20);
}

static IntPtr[] GetLayouts() { var list = new IntPtr[64]; var n = GetKeyboardLayoutList(list.Length, list); return list[..n]; }

[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll")] static extern bool UnloadKeyboardLayout(IntPtr hkl);
[DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n, IntPtr[] list);
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] static extern uint MapVirtualKey(uint c, uint t);
[DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int s);
[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public U U; }
[StructLayout(LayoutKind.Explicit)] struct U { [FieldOffset(0)] public KEYBDINPUT K; [FieldOffset(0)] public MOUSEINPUT M; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk; public ushort Scan; public uint Flags; public uint Time; public IntPtr Extra; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }

}
