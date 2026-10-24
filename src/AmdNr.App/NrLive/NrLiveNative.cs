// The three things the NRLive page asks of Windows itself: the windows open now, to pick one to capture; a
// hotkey that works while the game has focus, to scale and stop from inside it; and the stop key pressed on
// NRLive's behalf, when it runs elevated and this app may not end it.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AmdNr.App;

/// <summary>A window someone could mean: visible, titled, on the taskbar.</summary>
public sealed record OpenWindow(uint Pid, string Title, string Process)
{
    public override string ToString() => $"{Title}  ·  {Process}";
}

/// <summary>A game of the library in the menu of what to capture, by the name the library shows.</summary>
public sealed record LibraryGame(GameCard Card)
{
    public override string ToString() => Card.Name;
}

internal static class NrLiveNative
{
    /// <summary>The top-level windows a person would call open: visible, with a title, neither a tool window
    /// nor one Windows keeps cloaked (the suspended store apps), and not this app's own.</summary>
    public static IReadOnlyList<OpenWindow> Windows()
    {
        var found = new List<OpenWindow>();
        var self = (uint)Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, GwOwner) != 0) return true;
            if ((GetWindowLongPtrW(hwnd, GwlExStyle) & WsExToolWindow) != 0) return true;
            if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var length = GetWindowTextLengthW(hwnd);
            if (length == 0) return true;
            var title = new StringBuilder(length + 1);
            GetWindowTextW(hwnd, title, title.Capacity);
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == self) return true;
            string process;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                process = p.ProcessName + ".exe";
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return true; }
            found.Add(new OpenWindow(pid, title.ToString(), process));
            return true;
        }, 0);
        return found.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Presses and releases a hotkey as NRLive writes one ("ctrl+shift+a"). Input from this process
    /// reaches a hotkey an elevated NRLive registered, though Windows keeps it from that process's windows.</summary>
    public static bool Press(string hotkey)
    {
        if (!AmdNr.Core.NrLiveHotkey.TryParse(hotkey, out var mods, out var vk)) return false;
        var keys = new List<ushort>();
        if ((mods & AmdNr.Core.NrLiveHotkey.Control) != 0) keys.Add(0x11);
        if ((mods & AmdNr.Core.NrLiveHotkey.Alt) != 0) keys.Add(0x12);
        if ((mods & AmdNr.Core.NrLiveHotkey.Shift) != 0) keys.Add(0x10);
        if ((mods & AmdNr.Core.NrLiveHotkey.Win) != 0) keys.Add(0x5B);
        keys.Add((ushort)vk);
        var inputs = keys.Select(k => Key(k, up: false)).Concat(Enumerable.Reverse(keys).Select(k => Key(k, up: true))).ToArray();
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    private static Input Key(ushort vk, bool up) => new() { Type = 1, Ki = new KeyboardInput { Vk = vk, Flags = up ? 2u : 0u } };

    private const int GwOwner = 4, GwlExStyle = -20, DwmwaCloaked = 14;
    private const long WsExToolWindow = 0x80;

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, int cmd);
    [DllImport("user32.dll")] private static extern long GetWindowLongPtrW(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern int GetWindowTextLengthW(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Ki;
        // The union is as large as its mouse member; the keyboard one is shorter.
        private readonly long _pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint Extra;
    }
}

/// <summary>A hotkey that works whichever window has focus, registered on a thread of its own: RegisterHotKey
/// without a window posts to the thread that called it, and this one does nothing but wait for that. Disposed
/// when the combination changes or the page goes, which unregisters it.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private readonly Thread _thread;
    private uint _threadId;

    /// <summary>False when Windows refused the combination: another app registered it first.</summary>
    public bool Registered { get; private set; }

    public GlobalHotkey(uint modifiers, uint vk, Action pressed)
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            Registered = RegisterHotKey(0, 1, modifiers | 0x4000, vk); // MOD_NOREPEAT: held down, it fires once.
            ready.Set();
            if (!Registered) return;
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
                if (msg.Message == 0x0312) pressed(); // WM_HOTKEY
            UnregisterHotKey(0, 1);
        }) { IsBackground = true, Name = "NRLive hotkey" };
        _thread.Start();
        ready.Wait();
    }

    public void Dispose()
    {
        if (Registered) PostThreadMessageW(_threadId, 0x0012, 0, 0); // WM_QUIT
        Registered = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam, LParam;
        public uint Time;
        public int X, Y;
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessageW(out Msg msg, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint thread, uint msg, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
