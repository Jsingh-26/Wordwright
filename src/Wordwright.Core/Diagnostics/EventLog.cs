using System.Globalization;
using System.IO;
using System.Text;

namespace Wordwright.Core.Diagnostics;

/// <summary>
/// The events-only log (docs/ARCHITECTURE.md → Privacy): one file a day,
/// <c>wordwright-YYYYMMDD.log</c>, kept for <see cref="KeepDays"/> days.
/// <para>
/// It never holds content. Callers pass fixed event names, and an exception is
/// recorded by its type, HResult and stack frames only: an exception's message
/// can quote a file path or the data that failed to parse, so it is never
/// written. Logging can never fail the app; a write that fails is dropped.
/// </para>
/// </summary>
public sealed class EventLog
{
    public const int KeepDays = 7;

    private const string FilePrefix = "wordwright-";
    private const string FileExtension = ".log";

    private readonly string _directory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();

    public EventLog(string directory, Func<DateTimeOffset> clock)
    {
        _directory = directory;
        _clock = clock;
    }

    public string DirectoryPath => _directory;

    /// <summary>Records that <paramref name="eventName"/> happened. Pass a fixed
    /// name ("started", "hook reinstalled"), never anything the user typed.</summary>
    public void Write(string eventName) => Append($"{Stamp()} {eventName}");

    /// <summary>Records an exception without its message.</summary>
    public void Write(string eventName, Exception exception)
    {
        var text = new StringBuilder();
        text.Append(Stamp()).Append(' ').Append(eventName);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine()
                .Append("  ")
                .Append(current.GetType().FullName)
                .Append(" (HResult 0x")
                .Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture))
                .Append(')');

            if (current.StackTrace is { } stackTrace)
            {
                foreach (var frame in stackTrace.Split('\n'))
                {
                    text.AppendLine().Append("  ").Append(frame.TrimEnd('\r'));
                }
            }
        }

        Append(text.ToString());
    }

    /// <summary>Deletes the day files older than <see cref="KeepDays"/> days.</summary>
    public void Prune()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            var oldestKept = _clock().Date.AddDays(-(KeepDays - 1));
            foreach (var path in Directory.EnumerateFiles(_directory, FilePrefix + "*" + FileExtension))
            {
                var name = Path.GetFileNameWithoutExtension(path)[FilePrefix.Length..];
                if (DateTime.TryParseExact(
                        name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    && day < oldestKept)
                {
                    File.Delete(path);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string Stamp() => _clock().ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    private void Append(string entry)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                var day = _clock().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                File.AppendAllText(
                    Path.Combine(_directory, FilePrefix + day + FileExtension),
                    entry + Environment.NewLine);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
