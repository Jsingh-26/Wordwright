using System.Diagnostics;
using System.Runtime.InteropServices;
using Wordwright.Platform.Keyboard;

// Diagnostics harness for Wordwright.Platform.Keyboard.KeyboardHook: not part of
// the app. Real typing cannot be synthesised and the production hook ignores
// synthesised input, so the harness switches that filter off and feeds the hook
// with SendInput, reporting which events arrived (never the text itself).
//
//   dotnet run --project scripts/HookHarness                        (filter off)
//   dotnet run --project scripts/HookHarness -- --production-filter (the real thing)
//   dotnet run --project scripts/HookHarness -- --exclude-foreground

var ignoreInjected = args.Contains("--production-filter");
var excluded = args.Where(a => a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).ToList();
if (args.Contains("--exclude-foreground"))
{
    excluded.Add(ForegroundProcessName());
}

using var hook = new KeyboardHook(excluded, ignoreInjected);

var typed = new List<char>();
var backspaces = 0;
var clears = 0;

hook.CharacterTyped += (_, c) => typed.Add(c);
hook.BackspacePressed += (_, _) => Interlocked.Increment(ref backspaces);
hook.BufferCleared += (_, _) => Interlocked.Increment(ref clears);

hook.Start();
Console.WriteLine($"foreground : {ForegroundProcessName()}");
Console.WriteLine($"excluded   : [{string.Join(", ", excluded)}]");
Console.WriteLine($"filter     : ignoreInjected={ignoreInjected}");
Console.WriteLine($"install    : installed={hook.IsInstalled} running={hook.IsRunning}");
Thread.Sleep(500);

Console.WriteLine($"probe      : ToUnicodeEx('S')={Probe(0x53)} ToUnicodeEx(';')={Probe(0xBA)}");

typed.Clear();
SendKeys([0xBA, 0x53, 0x49, 0x47]);          // ; s i g
Thread.Sleep(700);
Console.WriteLine($"1 characters: {typed.Count} [{string.Join("", typed)}]  expected 4");

typed.Clear();
SendKey(0x08);                                // backspace
Thread.Sleep(400);
Console.WriteLine($"2 backspaces: {backspaces}  expected 1");

var before = clears;
SendKey(0x0D);                                // enter
Thread.Sleep(400);
Console.WriteLine($"3 clear Enter : {clears - before}  expected 1");

before = clears;
ClickLeft();
Thread.Sleep(400);
Console.WriteLine($"4 clear click : {clears - before}  expected 1");

before = clears;
SendKey(0x1B);                                // escape
Thread.Sleep(400);
Console.WriteLine($"5 clear Escape: {clears - before}  expected 1");

before = clears;
SendKey(0x26);                                // up arrow
Thread.Sleep(400);
Console.WriteLine($"6 clear Arrow : {clears - before}  expected 1");

typed.Clear();
Console.WriteLine($"7 excluded app in front: {excluded.Contains(ForegroundProcessName())}");
SendKeys([0x41, 0x42, 0x43]);                 // a b c
Thread.Sleep(600);
Console.WriteLine($"  characters while in front: {typed.Count}  expected 0");

hook.Stop();
Console.WriteLine($"stopped    : running={hook.IsRunning} installed={hook.IsInstalled}");

// A restarted hook must report again: Stop() completes the queue it was using.
typed.Clear();
backspaces = 0;
hook.Start();
Thread.Sleep(400);
SendKeys([0x58, 0x59]);            // x y
SendKey(0x08);
Thread.Sleep(600);
Console.WriteLine($"8 after restart: characters={typed.Count} [{string.Join("", typed)}] backspaces={backspaces}  expected 2 and 1");
hook.Stop();

static string ForegroundProcessName()
{
    var window = GetForegroundWindow();
    _ = GetWindowThreadProcessId(window, out var pid);
    try { return Process.GetProcessById((int)pid).ProcessName + ".exe"; } catch { return "unknown"; }
}

static ushort Scan(ushort virtualKey)
{
    var scan = MapVirtualKey(virtualKey, 0);   // MAPVK_VK_TO_VSC
    return scan == 0 ? (ushort)0 : (ushort)scan;
}

static void SendKeys(ushort[] virtualKeys)
{
    foreach (var key in virtualKeys)
    {
        SendKey(key);
    }
}

static void SendKey(ushort virtualKey)
{
    var scanCode = Scan(virtualKey);
    var inputs = new INPUT[2];
    inputs[0].Type = 1; inputs[0].Union.Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, ScanCode = scanCode };
    inputs[1].Type = 1; inputs[1].Union.Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, ScanCode = scanCode, Flags = 2 };
    _ = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    Thread.Sleep(40);
}

static void ClickLeft()
{
    var inputs = new INPUT[2];
    inputs[0].Type = 0; inputs[0].Union.Mouse = new MOUSEINPUT { Flags = 0x0002 };
    inputs[1].Type = 0; inputs[1].Union.Mouse = new MOUSEINPUT { Flags = 0x0004 };
    _ = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
}

// Mirrors the hook's own translation call, to tell a harness problem from a hook problem.
static string Probe(ushort virtualKey)
{
    var state = new byte[256];
    if (!GetKeyboardState(state)) { return "no state"; }
    var buffer = new char[8];
    var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out _));
    var written = ToUnicodeEx(virtualKey, Scan(virtualKey), state, buffer, buffer.Length, 0, layout);
    return written == 1 ? $"'{buffer[0]}'" : $"written={written}";
}

[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
[DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
[DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
[DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] state);
[DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint threadId);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, char[] buffer, int size, uint flags, IntPtr layout);

[StructLayout(LayoutKind.Sequential)]
struct INPUT { public uint Type; public InputUnion Union; }

[StructLayout(LayoutKind.Explicit)]
struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT Mouse;
    [FieldOffset(0)] public KEYBDINPUT Keyboard;
}

[StructLayout(LayoutKind.Sequential)]
struct MOUSEINPUT { public int X; public int Y; public uint MouseData; public uint Flags; public uint Time; public IntPtr ExtraInfo; }

[StructLayout(LayoutKind.Sequential)]
struct KEYBDINPUT { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
