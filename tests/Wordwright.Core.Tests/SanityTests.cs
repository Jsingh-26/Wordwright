using FluentAssertions;

namespace Wordwright.Core.Tests;

// Placeholder until P1.4 adds the first real Core code (SettingsStore).
// Proves the xUnit + FluentAssertions harness is wired up.

public class SanityTests
{
    [Fact]
    public void Test_framework_runs()
    {
        1.Should().Be(1);
    }
}