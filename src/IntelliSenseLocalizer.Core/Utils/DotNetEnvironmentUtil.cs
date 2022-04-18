using System.Diagnostics;
using System.Text.RegularExpressions;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public static class DotNetEnvironmentUtil
{
    public static IEnumerable<ApplicationPackDescriptor> GetAllApplicationPacks()
    {
        foreach (var sdkPath in GetAllInstalledSDKPaths())
        {
            var packRoot = GetSDKPackRoot(sdkPath);

            foreach (var item in GetAllApplicationPacks(packRoot))
            {
                yield return item;
            }
        }
    }

    public static IEnumerable<ApplicationPackDescriptor> GetAllApplicationPacks(string? packRoot)
    {
        if (!Directory.Exists(packRoot))
        {
            yield break;
        }

        //loop for directory like C:\Program Files\dotnet\packs\*.App.Ref
        foreach (var appPackRefDirectory in Directory.EnumerateDirectories(packRoot, "*.App.Ref", SearchOption.TopDirectoryOnly))
        {
            var appPackName = Path.GetFileName(appPackRefDirectory);

            yield return new ApplicationPackDescriptor(appPackName, appPackRefDirectory);
        }
    }

    #region Base

    public static string[] GetAllInstalledSDKPaths()
    {
        var proccessStartInfo = new ProcessStartInfo("dotnet", "--list-sdks")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        var process = Process.Start(proccessStartInfo);

        var output = process!.StandardOutput.ReadToEnd();
        var sdkPaths = Regex.Matches(output, "\\[(.+)\\]")
                            .Where(m => m.Groups.Count > 1)
                            .Select(m => m.Groups[1].Value.TrimEnd('/').TrimEnd('\\'))
                            .Distinct()
                            .ToArray();

        return sdkPaths;
    }

    public static string? GetSDKPackRoot(string sdkPath)
    {
        var pre = Path.GetDirectoryName(sdkPath);
        if (string.IsNullOrEmpty(pre))
        {
            return null;
        }
        return Path.Combine(pre, "packs");
    }

    #endregion Base
}
