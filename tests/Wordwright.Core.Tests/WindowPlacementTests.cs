using FluentAssertions;
using Wordwright.Core.Settings;

namespace Wordwright.Core.Tests;

public class WindowPlacementTests
{
    /// <summary>A 1920×1080 screen with a 40 px taskbar along the bottom.</summary>
    private static readonly WindowArea WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void AWindowThatAlreadyFits_isLeftAlone()
    {
        var saved = new WindowPlacement { Left = 120, Top = 80, Width = 1040, Height = 720 };

        var clamped = saved.ClampTo(WorkArea);

        clamped.Should().Be(saved);
    }

    [Fact]
    public void DefaultPlacement_isTheDesignedSize()
    {
        var placement = new WindowPlacement();

        placement.Width.Should().Be(1040);
        placement.Height.Should().Be(720);
        placement.Maximized.Should().BeFalse();
    }

    [Theory]
    [InlineData(400, 300)]      // dragged smaller than the minimum
    [InlineData(0, 0)]
    [InlineData(-100, -100)]
    public void AWindowBelowTheMinimum_growsTo800By600(double width, double height)
    {
        var saved = new WindowPlacement { Width = width, Height = height };

        var clamped = saved.ClampTo(WorkArea);

        clamped.Width.Should().Be(WindowPlacement.MinimumWidth);
        clamped.Height.Should().Be(WindowPlacement.MinimumHeight);
    }

    [Fact]
    public void AWindowSavedOnABiggerMonitor_shrinksToThisWorkArea()
    {
        var saved = new WindowPlacement { Width = 2560, Height = 1440 };

        var clamped = saved.ClampTo(WorkArea);

        clamped.Width.Should().Be(WorkArea.Width);
        clamped.Height.Should().Be(WorkArea.Height);
    }

    [Theory]
    [InlineData(1500, 100, 880, 100)]        // right edge past the screen
    [InlineData(100, 800, 100, 320)]         // bottom edge past the taskbar
    [InlineData(-600, -400, 0, 0)]           // dragged off the top-left
    public void AWindowPartlyOffScreen_movesFullyInside(
        double left, double top, double expectedLeft, double expectedTop)
    {
        var saved = new WindowPlacement { Left = left, Top = top, Width = 1040, Height = 720 };

        var clamped = saved.ClampTo(WorkArea);

        clamped.Left.Should().Be(expectedLeft);
        clamped.Top.Should().Be(expectedTop);
    }

    [Fact]
    public void AMonitorToTheLeft_isAllowedToHaveNegativeCoordinates()
    {
        var leftHandScreen = new WindowArea(-1920, -120, 1920, 1040);
        var saved = new WindowPlacement { Left = -1800, Top = 40, Width = 1040, Height = 720 };

        var clamped = saved.ClampTo(leftHandScreen);

        clamped.Should().Be(saved);
        clamped.Left.Should().BeNegative();
    }

    [Fact]
    public void AWorkAreaSmallerThanTheMinimum_keepsTheMinimumWidthButFitsTheHeight()
    {
        var tiny = new WindowArea(0, 0, 640, 480);
        var saved = new WindowPlacement { Left = 100, Top = 100, Width = 1040, Height = 720 };

        var clamped = saved.ClampTo(tiny);

        clamped.Width.Should().Be(WindowPlacement.MinimumWidth);
        clamped.Height.Should().Be(480);
        clamped.Left.Should().Be(0);
        clamped.Top.Should().Be(0);
    }

    [Fact]
    public void ALaptopAt125Percent_getsAWindowThatFitsAboveTheTaskbar()
    {
        // 1366×768 at 125 %: 1092.8 × 614.4 DIPs, about 582 tall above the taskbar.
        var laptop = new WindowArea(0, 0, 1092.8, 582.4);

        var clamped = new WindowPlacement().ClampTo(laptop);

        clamped.Height.Should().Be(582.4);
        (clamped.Top + clamped.Height).Should().BeLessThanOrEqualTo(laptop.Bottom);
        WindowPlacement.MinimumHeightFor(laptop).Should().Be(582.4);
    }

    [Fact]
    public void MinimumHeightFor_aTallWorkArea_isTheDesignedMinimum()
    {
        WindowPlacement.MinimumHeightFor(WorkArea).Should().Be(WindowPlacement.MinimumHeight);
    }

    /// <summary>The primary 1920×1040 work area, and a second monitor to its right.</summary>
    private static readonly WindowArea[] TwoMonitors =
    [
        WorkArea,
        new WindowArea(1920, 0, 2560, 1400),
    ];

    [Fact]
    public void PickWorkArea_aWindowClosedOnTheSecondMonitor_staysThere()
    {
        var saved = new WindowPlacement { Left = 2300, Top = 200, Width = 1040, Height = 720 };

        saved.PickWorkArea(TwoMonitors).Should().Be(TwoMonitors[1]);
        saved.ClampTo(saved.PickWorkArea(TwoMonitors)).Should().Be(saved);
    }

    [Fact]
    public void PickWorkArea_aWindowAcrossBothMonitors_goesWhereMostOfItIs()
    {
        var mostlyRight = new WindowPlacement { Left = 1700, Top = 100, Width = 1040, Height = 720 };
        var mostlyLeft = new WindowPlacement { Left = 1200, Top = 100, Width = 1040, Height = 720 };

        mostlyRight.PickWorkArea(TwoMonitors).Should().Be(TwoMonitors[1]);
        mostlyLeft.PickWorkArea(TwoMonitors).Should().Be(TwoMonitors[0]);
    }

    [Fact]
    public void PickWorkArea_whenThatMonitorIsGone_takesTheNearestOne()
    {
        // Saved on a monitor that sat to the right of the second one.
        var saved = new WindowPlacement { Left = 5000, Top = 100, Width = 1040, Height = 720 };

        saved.PickWorkArea(TwoMonitors).Should().Be(TwoMonitors[1]);
    }

    [Fact]
    public void PickWorkArea_withOneMonitor_isThatMonitor()
    {
        var saved = new WindowPlacement { Left = -3000, Top = -3000 };

        saved.PickWorkArea([WorkArea]).Should().Be(WorkArea);
    }

    [Fact]
    public void TheMaximisedState_survivesClamping()
    {
        var saved = new WindowPlacement { Left = -500, Top = -500, Maximized = true };

        var clamped = saved.ClampTo(WorkArea);

        clamped.Maximized.Should().BeTrue();
        clamped.Left.Should().Be(0);
        clamped.Top.Should().Be(0);
    }

}
