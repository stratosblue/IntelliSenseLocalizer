using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public static class DotNetEnvironmentUtil
{
    public static IEnumerable<ApplicationPackDescriptor> GetAllInstalledApplicationPacks()
    {
        foreach (var sdkPath in GetAllInstalledSDKPaths())
        {
            var packRoot = GetSDKPackRoot(sdkPath);
            if (!Directory.Exists(packRoot))
            {
                continue;
            }

            foreach (var item in GetApplicationPacks(packRoot))
            {
                yield return item;
            }
        }
    }

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

    public static IEnumerable<ApplicationPackDescriptor> GetApplicationPacks(string packRoot)
    {
        foreach (var appPackRefDirectory in Directory.EnumerateDirectories(packRoot, "*.App.Ref", SearchOption.TopDirectoryOnly))
        {
            var appPackName = Path.GetFileName(appPackRefDirectory);

            var versions = Directory.EnumerateDirectories(appPackRefDirectory, "*", SearchOption.TopDirectoryOnly)
                                    .Select(path => (path, Version.TryParse(Path.GetFileName(path), out var version) ? version : null))
                                    .Where(m => m.Item2 is not null)
                                    .ToArray()!;

            foreach (var (packVersionPath, version) in versions)
            {
                var packRefPath = Path.Combine(packVersionPath, "ref");
                if (!Directory.Exists(packRefPath))
                {
                    continue;
                }
                var applicationPackDescriptor = new ApplicationPackDescriptor(appPackName, appPackRefDirectory, version);

                foreach (var frameworkMonikerPath in Directory.EnumerateDirectories(packRefPath, "*", SearchOption.TopDirectoryOnly))
                {
                    var frameworkMoniker = Path.GetFileName(frameworkMonikerPath);

                    var applicationPackRefDescriptor = new ApplicationPackRefDescriptor(appPackName, version, frameworkMoniker, frameworkMonikerPath);

                    applicationPackDescriptor.PackRefs.Add(applicationPackRefDescriptor);

                    foreach (var intelliSenseFilePath in Directory.EnumerateFiles(frameworkMonikerPath, "*.xml", SearchOption.TopDirectoryOnly))
                    {
                        var intelliSenseName = Path.GetFileNameWithoutExtension(intelliSenseFilePath);
                        var intelliSenseFileName = Path.GetFileName(intelliSenseFilePath);
                        applicationPackRefDescriptor.IntelliSenseFiles.Add(new IntelliSenseFileDescriptor(applicationPackRefDescriptor, intelliSenseName, intelliSenseFileName, intelliSenseFilePath));
                    }
                }

                yield return applicationPackDescriptor;
            }
        }
    }

    public static IEnumerable<(string Locale, ApplicationPackDescriptor Descriptor)> GetLocalizedApplicationPacks(string packRoot)
    {
        foreach (var appPackRefDirectory in Directory.EnumerateDirectories(packRoot, "*.App.Ref", SearchOption.TopDirectoryOnly))
        {
            var appPackName = Path.GetFileName(appPackRefDirectory);

            var versions = Directory.EnumerateDirectories(appPackRefDirectory, "*", SearchOption.TopDirectoryOnly)
                                    .Select(path => (path, Version.TryParse(Path.GetFileName(path), out var version) ? version : null))
                                    .Where(m => m.Item2 is not null)
                                    .ToArray()!;

            foreach (var (packVersionPath, version) in versions)
            {
                var packRefPath = Path.Combine(packVersionPath, "ref");
                if (!Directory.Exists(packRefPath))
                {
                    continue;
                }

                foreach (var frameworkMonikerPath in Directory.EnumerateDirectories(packRefPath, "*", SearchOption.TopDirectoryOnly))
                {
                    foreach (var localizedPath in Directory.EnumerateDirectories(frameworkMonikerPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        var locale = Path.GetFileName(localizedPath);
                        try
                        {
                            CultureInfo.GetCultureInfo(locale);
                        }
                        catch
                        {
                            continue;
                        }
                        var applicationPackDescriptor = new ApplicationPackDescriptor(appPackName, appPackRefDirectory, version);
                        var frameworkMoniker = Path.GetFileName(frameworkMonikerPath);

                        var applicationPackRefDescriptor = new ApplicationPackRefDescriptor(appPackName, version, frameworkMoniker, localizedPath);

                        applicationPackDescriptor.PackRefs.Add(applicationPackRefDescriptor);

                        foreach (var intelliSenseFilePath in Directory.EnumerateFiles(localizedPath, "*.xml", SearchOption.TopDirectoryOnly))
                        {
                            var intelliSenseName = Path.GetFileNameWithoutExtension(intelliSenseFilePath);
                            var intelliSenseFileName = Path.GetFileName(intelliSenseFilePath);
                            applicationPackRefDescriptor.IntelliSenseFiles.Add(new IntelliSenseFileDescriptor(applicationPackRefDescriptor, intelliSenseName, intelliSenseFileName, intelliSenseFilePath));
                        }
                        yield return (locale, applicationPackDescriptor);
                    }
                }
            }
        }
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
}