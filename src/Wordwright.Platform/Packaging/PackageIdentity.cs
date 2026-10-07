using System.IO;
using System.Runtime.InteropServices;

namespace Wordwright.Platform.Packaging;

/// <summary>
/// Whether this build is running from an MSIX package. A packaged app is not
/// allowed the <c>HKCU</c> Run key the installer build uses, so the two builds
/// differ in how they start with Windows (docs/PLAN.md P3.6).
/// <para>
/// They also keep their data in different places. The installer build uses
/// <c>%AppData%\Wordwright</c>. The Store build uses its package's own
/// <c>LocalState</c> folder (<see cref="DataFolder"/>): Microsoft refused the
/// capability that would let it write the real <c>%AppData%</c>, and anything it
/// wrote there would be redirected somewhere Explorer cannot see
/// (docs/PLAN.md P14.1).
/// </para>
/// </summary>
public static class PackageIdentity
{
    /// <summary><c>APPMODEL_ERROR_NO_PACKAGE</c>: what Windows answers when the
    /// process has no package identity, i.e. the installer build.</summary>
    private const int NoPackage = 15700;

    private const int Success = 0;
    private const int InsufficientBuffer = 122;

    public static bool IsPackaged { get; } = Detect();

    /// <summary>
    /// The Store build's data folder,
    /// <c>%LocalAppData%\Packages\&lt;package family name&gt;\LocalState</c>,
    /// the folder Windows gives every package for its own files. It is not
    /// redirected, so "Open data folder" shows the real files, and Windows removes
    /// it when the app is uninstalled. Null for the installer build.
    /// </summary>
    public static string? DataFolder { get; } = IsPackaged ? FindDataFolder() : null;

    private static bool Detect()
    {
        // Called with no buffer on purpose: a packaged app answers
        // "insufficient buffer", an unpackaged one "no package".
        var length = 0u;

        return GetCurrentPackageFullName(ref length, null) != NoPackage;
    }

    private static string? FindDataFolder()
    {
        var length = 0u;
        if (GetCurrentPackageFamilyName(ref length, null) != InsufficientBuffer)
        {
            return null;
        }

        var name = new char[length];
        if (GetCurrentPackageFamilyName(ref length, name) != Success)
        {
            return null;
        }

        // length counts the terminating null.
        var familyName = new string(name, 0, (int)length - 1);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", familyName, "LocalState");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength, char[]? packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(
        ref uint packageFamilyNameLength, char[]? packageFamilyName);
}
