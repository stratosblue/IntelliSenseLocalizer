using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

using Cuture.Http;

using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    public const string LocalizedIntelliSenseFilePacksReleaseName = "LocalizedIntelliSenseFilePacks";

    private static Command BuildInstallCommand()
    {
        var installCommand = new Command("install", Resources.StringCMDInstallDescription);
        var sourceOption = new Option<string>(new[] { "-s", "--source" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDInstallOptionSourceDescription);
        var targetOption = new Option<string>(new[] { "-t", "--target" }, () => LocalizerEnvironment.DefaultSdkRoot, Resources.StringCMDInstallOptionTargetDescription);

        installCommand.AddOption(sourceOption);
        installCommand.AddOption(targetOption);

        installCommand.SetHandler((string source, string target) =>
        {
            if (source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                InstallFromUrlAsync(source, target, null).Wait();
            }
            else
            {
                InstallFromZipArchiveFile(source, target);
            }
        }, sourceOption, targetOption);

        {
            var monikerOption = new Option<string>(new[] { "-m", "--moniker" }, Resources.StringCMDInstallAutoOptionMonikerDescription);
            var localeOption = new Option<string>(new[] { "-l", "--locale" }, () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDInstallOptionLocaleDescription);
            var contentCompareTypeOption = new Option<ContentCompareType>(new[] { "-cc", "--content-compare" }, () => ContentCompareType.None, Resources.StringCMDBuildOptionContentCompareDescription);

            var autoInstallCommand = new Command("auto", Resources.StringCMDInstallAutoInstallDescription);
            autoInstallCommand.AddOption(targetOption);
            autoInstallCommand.AddOption(monikerOption);
            autoInstallCommand.AddOption(localeOption);
            autoInstallCommand.AddOption(contentCompareTypeOption);

            autoInstallCommand.SetHandler<string, string, string, ContentCompareType>((string target, string moniker, string locale, ContentCompareType contentCompareType) =>
            {
                var applicationPacks = DotNetEnvironmentUtil.GetAllApplicationPacks(DotNetEnvironmentUtil.GetSDKPackRoot(target)).ToArray();

                if (applicationPacks.Length == 0)
                {
                    WriteMessageAndExit($"no sdk found in \"{target}\"");
                    return;
                }

                if (PathAuthorityCheck(target))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(moniker))
                {
                    var maxVersion = applicationPacks.Max(m => m.Versions.Max(m => m.Version))!;
                    moniker = applicationPacks.SelectMany(m => m.Versions).Where(m => m.Version == maxVersion).First().Monikers.FirstOrDefault()?.Moniker ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(moniker))
                {
                    WriteMessageAndExit("can not select moniker automatic. please specify moniker.");
                    return;
                }

                try
                {
                    CultureInfo.GetCultureInfo(locale);
                }
                catch
                {
                    WriteMessageAndExit($"\"{locale}\" is not a effective locale.");
                    throw;
                }

                InstallFromGithubAsync(target, moniker, locale, contentCompareType).Wait();
            }, targetOption, monikerOption, localeOption, contentCompareTypeOption);

            installCommand.Add(autoInstallCommand);
        }

        return installCommand;
    }

    private static async Task InstallFromGithubAsync(string target, string moniker, string locale, ContentCompareType contentCompareType)
    {
        var contentCompare = contentCompareType.ToString();
        try
        {
            var assetsInfos = await FindAssetsAtReleasesAsync(moniker);
            if (assetsInfos.FirstOrDefault(m => m.Name.Contains(locale, StringComparison.OrdinalIgnoreCase) && m.Name.Contains(contentCompare, StringComparison.OrdinalIgnoreCase)) is not AssetsInfo targetAssetsInfo)
            {
                Console.WriteLine($"Not found {moniker}@{locale}@{contentCompareType} at github. Please build it yourself.");
                Environment.Exit(1);
                return;
            }
            await InstallFromUrlAsync(targetAssetsInfo.DownloadUrl, target, targetAssetsInfo.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Load from github fail. {ex.Message}.");
            Environment.Exit(1);
            throw;
        }

        static async Task<AssetsInfo[]> FindAssetsAtReleasesAsync(string moniker)
        {
            for (int pageIndex = 1; pageIndex < 10; pageIndex++)
            {
                var url = $"https://api.github.com/repos/stratosblue/intellisenselocalizer/releases?page={pageIndex}";
                using var response = await url.CreateHttpRequest().UseUserAgent(UserAgents.EdgeChromium).GetAsJsonDocumentAsync();
                var releases = response!.RootElement.EnumerateArray();
                if (!releases.Any(IsLocalizedIntelliSenseFilePacksRelease))
                {
                    continue;
                }
                var assets = releases.First(IsLocalizedIntelliSenseFilePacksRelease).GetProperty("assets").EnumerateArray();

                return assets.Select(m => new AssetsInfo(m.GetProperty("name").GetString()!, m.GetProperty("browser_download_url").GetString()!)).ToArray();
            }

            return Array.Empty<AssetsInfo>();

            bool IsLocalizedIntelliSenseFilePacksRelease(System.Text.Json.JsonElement jsonElement)
            {
                return jsonElement.GetProperty("name").GetString().EqualsOrdinalIgnoreCase($"{LocalizedIntelliSenseFilePacksReleaseName}-{moniker}");
            }
        }
    }

    private static async Task InstallFromUrlAsync(string downloadUrl, string target, string? fileName = null)
    {
        Console.WriteLine($"Start download {downloadUrl}");

        //直接内存处理，不缓存了
        using var httpOperationResult = await downloadUrl.CreateHttpRequest().AutoRedirection().UseUserAgent(UserAgents.EdgeChromium).TryGetAsBytesAsync();

        if (httpOperationResult.Exception is not null)
        {
            throw httpOperationResult.Exception;
        }

        if ((string.IsNullOrEmpty(fileName)
             && !TryGetFileName(httpOperationResult.ResponseMessage, out fileName))
            || !TryGetArchivePackageInfo(Path.GetFileNameWithoutExtension(fileName), out var moniker, out var locale, out var contentCompareType))
        {
            Console.WriteLine("can not get correct file name.");
            Environment.Exit(1);
            return;
        }

        var data = httpOperationResult.Data!;
        using var zipArchive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read, true);

        InstallFromZipArchive(target, moniker, locale, zipArchive);

        static bool TryGetFileName(HttpResponseMessage? responseMessage, [NotNullWhen(true)] out string? fileName)
        {
            fileName = null;
            if (responseMessage is null)
            {
                return false;
            }
            var contentDisposition = responseMessage.Headers.GetValues("Content-Disposition").FirstOrDefault() ?? string.Empty;

            if (Regex.Match(contentDisposition, @"filename=(.+?)\.zip") is not Match match
                || match.Groups.Count < 2)
            {
                return false;
            }
            fileName = match.Groups[1].Value;
            return true;
        }
    }

    private static void InstallFromZipArchive(string target, string moniker, string locale, ZipArchive zipArchive)
    {
        var packRoot = DotNetEnvironmentUtil.GetSDKPackRoot(target);
        if (!Directory.Exists(packRoot))
        {
            Console.WriteLine($"not found any pack at the target SDK folder {target}. please check input.");
            Environment.Exit(1);
        }

        var applicationPackRefs = DotNetEnvironmentUtil.GetAllApplicationPacks(packRoot)
                                                       .SelectMany(m => m.Versions)
                                                       .SelectMany(m => m.Monikers)
                                                       .Where(m => m.Moniker.EqualsOrdinalIgnoreCase(moniker))
                                                       .SelectMany(m => m.Refs)
                                                       .Where(m => m.Culture is null)
                                                       .ToArray();

        var packEntryGroups = zipArchive.Entries.Where(m => m.Name.IsNotNullOrEmpty())
                                                .GroupBy(m => m.FullName.Split('\\', StringSplitOptions.RemoveEmptyEntries)[0])
                                                .ToDictionary(m => m.Key, m => m.ToArray(), StringComparer.OrdinalIgnoreCase);

        var count = 0;
        foreach (var applicationPackRef in applicationPackRefs)
        {
            var packName = applicationPackRef.OwnerMoniker.OwnerVersion.OwnerPack.Name;
            if (!packEntryGroups.TryGetValue(packName, out var entries)
                || entries.Length == 0)
            {
                continue;
            }

            var rootPath = Path.Combine(applicationPackRef.RootPath, locale);

            DirectoryUtil.CheckDirectory(rootPath);

            foreach (var entry in entries)
            {
                var targetFile = Path.Combine(rootPath, entry.Name);
                Console.WriteLine($"Create File: {targetFile}");
                entry.ExtractToFile(targetFile, true);
                count++;
            }
        }

        Console.WriteLine($"Install done. {count} item copyed.");
    }

    private static void InstallFromZipArchiveFile(string sourceFile, string target)
    {
        try
        {
            if (!TryGetArchivePackageInfo(Path.GetFileNameWithoutExtension(sourceFile), out var moniker, out var locale, out var contentCompareType))
            {
                Console.WriteLine("The file name must be moniker@locale@ContentCompareType like net6.0@zh-cn@LocaleFirst.");
                Environment.Exit(1);
                return;
            }

            using var stream = File.OpenRead(sourceFile);
            ZipArchive zipArchive;
            try
            {
                zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            }
            catch
            {
                Console.WriteLine($"open \"{sourceFile}\" fail. confirm the file is a valid archive file.");
                Environment.Exit(1);
                return;
            }
            InstallFromZipArchive(target, moniker, locale, zipArchive);
        }
        catch (UnauthorizedAccessException ex)
        {
            RunAsAdminUtil.TryReRunAsAdmin(ex);
            return;
        }
    }

    private static bool PathAuthorityCheck(string targetPath)
    {
        var writeTestPath = Path.Combine(targetPath, ".write_test");
        try
        {
            File.WriteAllText(writeTestPath, "write_test");
            File.Delete(writeTestPath);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            RunAsAdminUtil.TryReRunAsAdmin(ex);
            return true;
        }
    }

    /// <summary>
    /// 必须为 net6.0@zh-cn@LocaleFirst 这种格式
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="moniker"></param>
    /// <param name="locale"></param>
    /// <param name="contentCompareType"></param>
    /// <returns></returns>
    private static bool TryGetArchivePackageInfo(string fileName, out string moniker, out string locale, out ContentCompareType contentCompareType)
    {
        var seg = fileName.Split('@');
        moniker = string.Empty;
        locale = string.Empty;
        contentCompareType = ContentCompareType.Default;
        if (seg.Length != 3)
        {
            return false;
        }

        try
        {
            CultureInfo.GetCultureInfo(seg[1]);
            locale = seg[1];
        }
        catch
        {
            return false;
        }

        moniker = seg[0];

        return Enum.TryParse(seg[2], out contentCompareType);
    }

    private record AssetsInfo(string Name, string DownloadUrl);
}
