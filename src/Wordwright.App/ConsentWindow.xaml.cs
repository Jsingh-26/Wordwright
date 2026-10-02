using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Wordwright.App.Ai;
using Wordwright.App.Controls;
using Wordwright.App.Resources;
using Wordwright.Core.Hardware;
using Wordwright.Core.Models;
using Wordwright.Platform.Hardware;

namespace Wordwright.App;

/// <summary>
/// "Turn on offline AI": what Wordwright would download, how long it should
/// take on this PC, what the model is good at, and what it needs
/// (docs/DESIGN.md §5, docs/PLAN.md P5.1). Nothing is downloaded from here yet —
/// the download itself arrives with P5.3 — and nothing leaves the machine.
/// </summary>
public partial class ConsentWindow : FluentWindow
{
    /// <summary>Memory the PC needs beyond the model's own requirement, the same
    /// headroom docs/MODELS.md asks of the recommendation.</summary>
    private const double RamHeadroomGB = 1;

    /// <summary>Disk the download needs, as a multiple of the file size.</summary>
    private const double DiskHeadroomFactor = 1.2;

    /// <summary>The smallest card that counts as "suitable for AI"
    /// (docs/MODELS.md → Hardware tiers).</summary>
    private const ulong GpuMemoryFloor = 6_000_000_000;

    /// <summary>The downloader, one per dialogue, so its HttpClient is reused.</summary>
    private readonly ModelDownloader _downloader = new();

    private CatalogEntry? _model;
    private ModelRecommendation? _recommendation;
    private HardwareProfile? _profile;
    private CancellationTokenSource? _download;
    private DateTimeOffset _startedAt;

    public ConsentWindow()
    {
        InitializeComponent();

        // WMI and DXGI take a moment, so the dialogue says it is checking first.
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var catalog = CatalogParser.Embedded();
        var profile = await Task.Run(HardwareProbe.Read);

        Checking.Visibility = Visibility.Collapsed;

        // Judged as the machine is now, not as it was when the app started
        // (docs/PLAN.md → "Maintainer requirement: startup resource eligibility").
        var availability = AiGate.Check(profile);

        if (!availability.IsAvailable)
        {
            ShowUnavailable(availability);
            return;
        }

        if (Recommender.Recommend(catalog, profile) is not { } recommendation)
        {
            ShowNothingToOffer(catalog, profile);
            return;
        }

        Show(recommendation, profile);
    }

    /// <summary>
    /// This PC cannot carry anything in the catalog, so nothing is offered. The
    /// wording names the resource that is short, and never asks the user to close
    /// anything to qualify.
    /// </summary>
    private void ShowUnavailable(AiAvailability availability)
    {
        NoOffer.Text = AiGate.Refusal(availability);
        NoOffer.Visibility = Visibility.Visible;

        // Nothing to download, and nothing may be added by hand either.
        PrimaryButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
    }

    private void Show(ModelRecommendation recommendation, HardwareProfile profile)
    {
        Recommendation.Visibility = Visibility.Visible;

        _recommendation = recommendation;
        _profile = profile;
        _model = recommendation.Model;

        var model = recommendation.Model;
        var tier = recommendation.Tier.ToCatalogName();
        var speed = model.SpeedFor(tier);

        ModelName.Text = Strings.Get("Ai.Rec.ModelLine", ("ModelName", model.DisplayName));
        MetaLine.Text = Strings.Get("Ai.Rec.MetaLine", ("Size", Gigabytes(model.SizeBytes)), ("License", model.License));
        YourPc.Text = Strings.Get(
            "Ai.Rec.YourPc",
            ("Ram", WholeGigabytes(profile.TotalRamBytes)),
            ("Cpu", Cpu(profile)),
            ("GpuSummary", GpuSummary(profile)));

        Ruler.Bands =
        [
            new RulerBand(
                Strings.Get("Ai.Rec.OneLine"),
                Range(recommendation.OneLine),
                recommendation.OneLine.FastestSeconds,
                recommendation.OneLine.SlowestSeconds),
            new RulerBand(
                Strings.Get("Ai.Rec.Paragraph"),
                Range(recommendation.ShortParagraph),
                recommendation.ShortParagraph.FastestSeconds,
                recommendation.ShortParagraph.SlowestSeconds),
        ];

        GoodAt.Text = Strings.Get("Ai.Rec.GoodAt", ("Strengths", string.Join(", ", model.Strengths)));
        NotSoGood.Text = Strings.Get("Ai.Rec.NotSoGood", ("Weaknesses", string.Join(", ", model.Weaknesses)));
        LoadNote.Text = Strings.Get("Ai.Rec.LoadNote", ("LoadSeconds", speed?.LoadSeconds ?? 0));

        if (recommendation.Tier == HardwareTier.Minimal)
        {
            Note.Text = Strings.Get("Ai.Rec.Minimal");
            Note.Visibility = Visibility.Visible;
        }
        else if (recommendation.Reason == StepDownReason.NotEnoughRam)
        {
            Note.Text = Strings.Get("Ai.Rec.StepDownRam", ("FreeRam", WholeGigabytes(profile.AvailableRamBytes)));
            Note.Visibility = Visibility.Visible;
        }

        ShowOtherOptions(recommendation);
    }

    /// <summary>The rest of the models that fit, with their own estimates, and a
    /// plain note on the ones bigger than the offer.</summary>
    private void ShowOtherOptions(ModelRecommendation recommendation)
    {
        if (recommendation.OtherOptions.Count == 0)
        {
            return;
        }

        var tier = recommendation.Tier.ToCatalogName();
        var rows = new List<OptionRow>();

        foreach (var model in recommendation.OtherOptions)
        {
            var speed = model.SpeedFor(tier);
            if (speed is null)
            {
                continue;
            }

            var oneLine = SpeedEstimator.EstimateRange(speed, EstimateJob.OneLine);
            var detail = $"{Gigabytes(model.SizeBytes)} · {Range(oneLine)}";

            if (model.SizeBytes > recommendation.Model.SizeBytes)
            {
                detail += " · " + Strings.Get("Ai.Rec.OtherSlow");
            }

            rows.Add(new OptionRow(model.DisplayName, detail));
        }

        if (rows.Count == 0)
        {
            return;
        }

        OtherList.ItemsSource = rows;
        OtherOptions.Visibility = Visibility.Visible;
    }

    /// <summary>No approved model fits: say which kind of "no" this is.</summary>
    private void ShowNothingToOffer(ModelCatalog catalog, HardwareProfile profile)
    {
        var approved = catalog.Models.Where(model => model.IsApproved).ToList();

        NoOffer.Text = approved.Count == 0
            ? Strings.Get("Ai.Rec.None")
            : Message(approved, profile);

        NoOffer.Visibility = Visibility.Visible;

        // There is nothing to download, so there is nothing to agree to.
        PrimaryButton.IsEnabled = false;
    }

    private static string Message(List<CatalogEntry> approved, HardwareProfile profile)
    {
        var smallest = approved.OrderBy(model => model.SizeBytes).First();

        if (profile.FreeDiskBytes < smallest.SizeBytes * DiskHeadroomFactor)
        {
            var needed = Gigabytes((long)(smallest.SizeBytes * DiskHeadroomFactor));
            var root = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) ?? "C:";

            return Strings.Get("Ai.Rec.NoDisk", ("Needed", needed), ("Drive", root.TrimEnd('\\')));
        }

        return Strings.Get("Ai.Rec.NoRam");
    }

    /// <summary>A range, or "30+" when it runs past the ruler's end.</summary>
    private static string Range(SpeedRange range) =>
        range.SlowestSeconds > TimeRuler.MaximumSeconds
            ? Strings.Get("Ai.Rec.RangeOver", ("Max", TimeRuler.MaximumSeconds))
            : Strings.Get("Ai.Rec.Range", ("Min", range.FastestSeconds), ("Max", range.SlowestSeconds));

    /// <summary>The processor's name without the trade marks and clock speed WMI
    /// reports with it: "Intel(R) Core(TM) i5-1235U CPU @ 1.30GHz" reads as
    /// "Intel Core i5-1235U", the way people write it.</summary>
    private static string Cpu(HardwareProfile profile) =>
        Regex.Replace(profile.CpuName, @"\s*\((R|TM|C)\)|\s+CPU.*$", "", RegexOptions.IgnoreCase).Trim()
            is { Length: > 0 } name ? name : "unknown processor";

    private static string GpuSummary(HardwareProfile profile) =>
        profile.Gpus.FirstOrDefault(gpu => gpu.DedicatedMemoryBytes >= GpuMemoryFloor) is { } suitable
            ? Strings.Get("Ai.Rec.GpuYes", ("GpuName", suitable.Name), ("Vram", WholeGigabytes(suitable.DedicatedMemoryBytes)))
            : Strings.Get("Ai.Rec.GpuNone");

    private static string Gigabytes(long bytes) => Format(bytes / 1_000_000_000.0);

    /// <summary>Memory and disk sizes read as whole gigabytes, as the shops and
    /// Windows write them.</summary>
    private static string WholeGigabytes(ulong bytes) => Format(bytes / 1_000_000_000.0);

    private static string Format(double gigabytes) =>
        gigabytes.ToString("0.#", CultureInfo.CurrentCulture) + " GB";

    private void OnPrimaryClicked(object sender, RoutedEventArgs e) => _ = StartDownloadAsync();

    /// <summary>"Not now" and "Cancel download" both leave, and a download in
    /// flight stops with its part file kept.</summary>
    private void OnSecondaryClicked(object sender, RoutedEventArgs e)
    {
        _download?.Cancel();
        Close();
    }

    /// <summary>
    /// Fetches the model the dialogue is offering, showing how far it has got
    /// (docs/PLAN.md P5.3). A download that stops keeps its part file, so the
    /// button it leaves behind offers to resume; one that arrives unverified is
    /// gone, so that button offers to start again.
    /// </summary>
    private async Task StartDownloadAsync()
    {
        if (_model is not { } model)
        {
            return;
        }

        _download?.Dispose();
        _download = new CancellationTokenSource();

        // Speed and time left are worked out from the clock, not from the
        // progress reports, which arrive unevenly.
        _startedAt = DateTimeOffset.Now;

        Recommendation.Visibility = Visibility.Collapsed;
        Problem.Visibility = Visibility.Collapsed;
        Busy.Visibility = Visibility.Collapsed;

        DownloadTitle.Text = Strings.Get("Ai.Dl.Title", ("ModelName", model.DisplayName));
        DownloadLine.Text = "";
        DownloadBar.Value = 0;
        Downloading.Visibility = Visibility.Visible;
        PrimaryButton.Visibility = Visibility.Collapsed;
        SecondaryButton.Content = Strings.Get("Ai.Dl.Cancel");
        SecondaryButton.IsEnabled = true;

        var folder = ((App)Application.Current).ModelsFolder;
        var progress = new Progress<DownloadProgress>(Report);

        DownloadFailure failure;
        try
        {
            failure = await _downloader.DownloadAsync(model, folder, progress, _download.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user: back to the offer, with the part file kept.
            ShowOffer();
            return;
        }

        Downloading.Visibility = Visibility.Collapsed;

        if (failure == DownloadFailure.None)
        {
            // Downloaded and verified. Register it and turn AI on so rewriting can
            // use it; measuring this PC's speed is the calibration step (P7.1).
            var app = (App)Application.Current;
            new InstalledModelStore(app.ModelsFolder).Add(new InstalledModel
            {
                Id = model.Id,
                File = ModelDownloader.FileNameFor(model),
                Sha256 = model.Sha256,
                Verified = true,
            });

            app.UpdateSettings(app.Settings with { AiEnabled = true, ActiveModelId = model.Id });

            Debug.WriteLine($"consent: {model.Id} downloaded and verified");
            Close();
            return;
        }

        ShowProblem(failure);
    }

    private void Report(DownloadProgress progress)
    {
        DownloadBar.Value = progress.TotalBytes > 0 ? progress.Fraction * 100 : 0;

        var done = progress.BytesDone;
        var total = progress.TotalBytes > 0 ? progress.TotalBytes : done;
        var secondsLeft = SecondsLeft(progress);

        DownloadLine.Text = Strings.Get(
            "Ai.Dl.Progress",
            ("Done", Size(done)),
            ("Total", Size(total)),
            ("Speed", Speed(progress)),
            ("TimeLeft", Duration(secondsLeft)));
    }

    /// <summary>The average speed so far, from the time the download has taken.</summary>
    private string Speed(DownloadProgress progress)
    {
        var seconds = (DateTimeOffset.Now - _startedAt).TotalSeconds;
        if (seconds <= 0 || progress.BytesDone <= 0)
        {
            return Size(0) + "/s";
        }

        return Size((long)(progress.BytesDone / seconds)) + "/s";
    }

    private double SecondsLeft(DownloadProgress progress)
    {
        var seconds = (DateTimeOffset.Now - _startedAt).TotalSeconds;
        if (seconds <= 0 || progress.BytesDone <= 0 || progress.TotalBytes <= 0)
        {
            return 0;
        }

        var perByte = seconds / progress.BytesDone;

        return perByte * (progress.TotalBytes - progress.BytesDone);
    }

    /// <summary>A size as people write it: "1.6 GB", "48 MB".</summary>
    private static string Size(long bytes) => bytes switch
    {
        >= 1_000_000_000 => (bytes / 1_000_000_000.0).ToString("0.#", CultureInfo.CurrentCulture) + " GB",
        >= 1_000_000 => (bytes / 1_000_000.0).ToString("0.#", CultureInfo.CurrentCulture) + " MB",
        >= 1_000 => (bytes / 1_000.0).ToString("0.#", CultureInfo.CurrentCulture) + " KB",
        _ => bytes + " bytes",
    };

    private static string Duration(double seconds) => seconds switch
    {
        <= 0 => "a moment",
        < 60 => $"{Math.Max(1, Math.Round(seconds)):0} seconds",
        _ => $"{Math.Max(1, Math.Round(seconds / 60)):0} minutes",
    };

    /// <summary>Back to the recommendation, as it was.</summary>
    private void ShowOffer()
    {
        if (_recommendation is { } recommendation)
        {
            Show(recommendation, _profile!);
        }

        Downloading.Visibility = Visibility.Collapsed;
        Busy.Visibility = Visibility.Collapsed;
        Problem.Visibility = Visibility.Collapsed;
        Recommendation.Visibility = Visibility.Visible;
        PrimaryButton.Visibility = Visibility.Visible;
        SecondaryButton.Content = Strings.Get("Ai.Rec.Secondary");
    }

    private void ShowProblem(DownloadFailure failure)
    {
        var corrupt = failure == DownloadFailure.Corrupt;

        Problem.Text = Strings.Get(corrupt ? "Ai.Verify.Failed" : "Ai.Dl.Failed");
        Problem.Visibility = Visibility.Visible;

        // A stopped download has a part file to carry on from; a corrupt one
        // was deleted, so there is nothing to resume.
        PrimaryButton.Content = Strings.Get(corrupt ? "Ai.Rec.Primary" : "Ai.Dl.Resume");
        PrimaryButton.Visibility = Visibility.Visible;
        SecondaryButton.Content = Strings.Get(corrupt ? "Ai.Rec.Secondary" : "Ai.Dl.Cancel");
    }

    /// <summary>
    /// Imports a model file the user already has (docs/PLAN.md P5.4): a GGUF
    /// whose hash the catalog knows counts as that model, anything else is a
    /// custom model taken on trust. The file moves into Wordwright's models
    /// folder, and the dialogue closes; turning AI on with it is the calibration
    /// step, which arrives with P7.1.
    /// </summary>
    private void OnImportClicked(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = Strings.Get("Ai.Rec.Import"),
            Filter = "*.gguf|*.gguf",
            CheckFileExists = true,
        };

        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        Import(picker.FileName);
    }

    /// <summary>The import itself, once a file has been chosen.</summary>
    private void Import(string path)
    {
        // Measured again here rather than trusting the window's own probe: the
        // dialogue can sit open for a while, and the requirement is that a change
        // since then cannot be bypassed (docs/PLAN.md → resource eligibility).
        if (!AiGate.Check(HardwareProbe.Read()).IsAvailable)
        {
            Busy.Visibility = Visibility.Collapsed;
            Problem.Text = Strings.Get("Ai.Unavailable.Import");
            Problem.Visibility = Visibility.Visible;
            return;
        }

        Busy.Text = Strings.Get("Ai.Verify");
        Busy.Visibility = Visibility.Visible;
        Problem.Visibility = Visibility.Collapsed;

        var app = (App)Application.Current;
        var (outcome, model, catalogEntry) = ModelImporter.Import(
            path, app.ModelsFolder, CatalogParser.Embedded());

        switch (outcome)
        {
            case ImportOutcome.Imported:
                new InstalledModelStore(app.ModelsFolder).Add(model!);
                app.UpdateSettings(app.Settings with { AiEnabled = true, ActiveModelId = model!.Id });
                Debug.WriteLine(
                    $"consent: imported {model!.Id} ({(catalogEntry is null ? "custom, unverified" : "verified")})");
                Close();
                break;

            case ImportOutcome.NotGguf:
                Busy.Visibility = Visibility.Collapsed;
                Problem.Text = Strings.Get("Ai.Import.NotModel");
                Problem.Visibility = Visibility.Visible;
                break;

            default:
                Busy.Visibility = Visibility.Collapsed;
                Problem.Text = Strings.Get("Ai.Import.Failed");
                Problem.Visibility = Visibility.Visible;
                break;
        }
    }

    /// <summary>One line under "Show other options".</summary>
    private sealed record OptionRow(string Label, string Detail);
}
