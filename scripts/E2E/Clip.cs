using System.Diagnostics;
using System.Text;
using Clipboard = System.Windows.Clipboard;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;

namespace E2E;

/// <summary>Clipboard access for the runner (STA thread), with retries because
/// another app or Wordwright itself may hold the clipboard for a moment.</summary>
internal static class Clip
{
    public static string HistoryNote { get; private set; } = "";

    public static string? GetText()
    {
        for (var i = 0; i < 20; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(100); }
        }
        return null;
    }

    public static void SetText(string text)
    {
        for (var i = 0; i < 20; i++)
        {
            try { Clipboard.SetDataObject(text, copy: true); return; }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(100); }
        }
    }

    /// <summary>Puts a large, many-format item on the clipboard, shaped like a big
    /// spreadsheet copy: tab-separated text, CSV and HTML. Returns the text length.</summary>
    public static int SetHeavy(int chars)
    {
        var row = "1234.56\tWidget\tNorth\t2026-10-03\r\n";
        var text = new StringBuilder(chars + row.Length);
        while (text.Length < chars) text.Append(row);
        var t = text.ToString();
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, t);
        data.SetData(DataFormats.CommaSeparatedValue, t.Replace('\t', ','));
        data.SetData(DataFormats.Html, "Version:0.9\r\nStartHTML:-1\r\nEndHTML:-1\r\nStartFragment:-1\r\nEndFragment:-1\r\n<table><tr><td>" + t[..Math.Min(t.Length, 200_000)] + "</td></tr></table>");
        for (var i = 0; i < 20; i++)
        {
            try { Clipboard.SetDataObject(data, copy: true); return t.Length; }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(100); }
        }
        return t.Length;
    }

    /// <summary>True when any Win+V history item holds the text, false when none does,
    /// null when the history cannot be read (HistoryNote says why).</summary>
    public static bool? HistoryContains(string text)
    {
        try
        {
            if (!Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled())
            {
                HistoryNote = "history is off";
                return null;
            }
            var result = Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync().AsTask().GetAwaiter().GetResult();
            if (result.Status != Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success)
            {
                HistoryNote = "history read refused: " + result.Status;
                return null;
            }
            var found = false;
            var n = 0;
            foreach (var item in result.Items)
            {
                n++;
                if (!item.Content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) continue;
                var s = item.Content.GetTextAsync().AsTask().GetAwaiter().GetResult();
                if (s.Contains(text, StringComparison.Ordinal)) found = true;
            }
            HistoryNote = $"{n} items read";
            return found;
        }
        catch (Exception ex)
        {
            HistoryNote = "history unreadable: " + ex.GetType().Name;
            return null;
        }
    }
}

/// <summary>Samples the system TCP and UDP tables (netstat -ano) every two
/// seconds and records any row owned by a Wordwright process under test.</summary>
internal sealed class NetWatch
{
    private Thread? _thread;
    private volatile bool _stop;
    public List<string> Seen { get; } = [];
    public int Samples { get; private set; }

    public void Start(HashSet<int> pids)
    {
        _thread = new Thread(() =>
        {
            while (!_stop)
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo("netstat", "-ano") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    Samples++;
                    int[] watch;
                    lock (pids) watch = pids.ToArray();
                    foreach (var line in output.Split('\n'))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4 && int.TryParse(parts[^1], out var pid) && watch.Contains(pid))
                            lock (Seen) Seen.Add(line.Trim());
                    }
                }
                catch { }
                for (var i = 0; i < 20 && !_stop; i++) Thread.Sleep(100);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public void Stop() { _stop = true; _thread?.Join(5000); }
}
