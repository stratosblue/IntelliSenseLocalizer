using System.Diagnostics.CodeAnalysis;

namespace IntelliSenseLocalizer.Utils;

internal static class VersionUtil
{
    public static bool TryParse(string versionString, [NotNullWhen(true)] out Version? version)
    {
        var index = versionString.IndexOf('-');
        if (index >= 0)
        {
            versionString = versionString[..index];
        }
        if (Version.TryParse(versionString, out version))
        {
            return true;
        }
        return false;
    }
}
