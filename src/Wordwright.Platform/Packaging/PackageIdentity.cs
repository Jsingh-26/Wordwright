using System.Runtime.InteropServices;

namespace Wordwright.Platform.Packaging;

/// <summary>
/// Whether this build is running from an MSIX package. A packaged app is not
/// allowed the <c>HKCU</c> Run key the installer build uses, so the two builds
/// differ in how they start with Windows (docs/PLAN.md P3.6).
/// <para>
/// Note for the P3.6 hand check: Windows may redirect a packaged app's AppData
/// into the package's own store, which would put the two builds' snippets in
/// different places. The check confirms where the packaged build's data lands.
/// </para>
/// </summary>
public static class PackageIdentity
{
    /// <summary><c>APPMODEL_ERROR_NO_PACKAGE</c>: what Windows answers when the
    /// process has no package identity, i.e. the installer build.</summary>
    private const int NoPackage = 15700;

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        // Called with no buffer on purpose: a packaged app answers
        // "insufficient buffer", an unpackaged one "no package".
        var length = 0u;

        return GetCurrentPackageFullName(ref length, null) != NoPackage;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength, char[]? packageFullName);
}
