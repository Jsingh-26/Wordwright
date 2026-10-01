using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows;
using Wpf.Ui.Controls;
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

        if (Recommender.Recommend(catalog, profile) is not { } recommendation)
        {
            ShowNothingToOffer(catalog, profile);
            return;
        }

        Show(recommendation, profile);
    }

    private void Show(ModelRecommendation recommendation, HardwareProfile profile)
    {
        Recommendation.Visibility = Visibility.Visible;

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

    private void OnNotNowClicked(object sender, RoutedEventArgs e) => Close();

    private void OnPrimaryClicked(object sender, RoutedEventArgs e)
    {
        // The download flow takes over this button in P5.3; until then, agreeing
        // simply closes the dialogue rather than pretending to fetch anything.
        Debug.WriteLine("consent: agreed to download (the download itself arrives in P5.3)");
        Close();
    }

    /// <summary>One line under "Show other options".</summary>
    private sealed record OptionRow(string Label, string Detail);
}
