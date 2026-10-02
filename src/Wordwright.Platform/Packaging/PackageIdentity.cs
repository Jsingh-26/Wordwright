using System.Runtime.InteropServices;

namespace Wordwright.Platform.Packaging;

/// <summary>
/// Whether this build is running from an MSIX package. A packaged app is not
/// allowed the <c>HKCU</c> Run key the installer build uses, so the two builds
/// differ in how they start with Windows (docs/PLAN.md P3.6).
/// <para>
/// Both builds keep their data in the real <c>%AppData%\Wordwright</c>: the
/// package turns file-system write virtualisation off (packaging/AppxManifest.xml,
/// docs/PLAN.md P12.2), so Windows does not redirect it into the package's store.
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
