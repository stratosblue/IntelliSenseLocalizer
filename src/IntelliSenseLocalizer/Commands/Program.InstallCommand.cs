using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Cuture.Http;

using IntelliSenseLocalizer.Nuget;
using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    public const string LocalizedIntelliSenseFilePacksReleaseName = "LocalizedIntelliSenseFilePacks";

    private static Command BuildInstallCommand()
    {
        var installCommand = new Command("install", Resources.StringCMDInstallDescription);
        var sourceOption = new Argument<string>("source", Resources.StringCMDInstallOptionSourceDescription);
        var targetOption = new Option<string>(new[] { "-t", "--target" }, () => LocalizerEnvironment.DefaultSdkRoot, Resources.StringCMDInstallOptionTargetDescription);
        var copyToNugetGlobalCacheOption = new Option<bool>(new[] { "-ctn", "--copy-to-nuget-global-cache" }, () => false, Resources.StringCMDInstallOptionCopyToNugetGlobalCacheDescription);

        installCommand.AddArgument(sourceOption);
        installCommand.AddOption(targetOption);
        installCommand.AddOption(copyToNugetGlobalCacheOption);

        installCommand.SetHandler((string source, string target, bool copyToNugetGlobalCache) =>
        {
            if (source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                InstallFromUrlAsync(downloadUrl: source, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache, fileName: null).Wait();
            }
            else
            {
                InstallFromZipArchiveFile(sourceFile: source, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache);
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
            autoInstallCommand.AddOption(copyToNugetGlobalCacheOption);

            autoInstallCommand.SetHandler<string, string, string, ContentCompareType, bool>((string target, string moniker, string locale, ContentCompareType contentCompareType, bool copyToNugetGlobalCache) =>
            {
                try
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

                    InstallFromGithubAsync(target, moniker, locale, contentCompareType, copyToNugetGlobalCache).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auto install failed: {ex.InnerException ?? ex}");
                    Console.WriteLine("press any key to continue");
                    Console.ReadKey();
                }
            }, targetOption, monikerOption, localeOption, contentCompareTypeOption, copyToNugetGlobalCacheOption);

            installCommand.Add(autoInstallCommand);
        }

        return installCommand;
    }

    private static async Task InstallFromGithubAsync(string target, string moniker, string locale, ContentCompareType contentCompareType, bool copyToNugetGlobalCache)
    {
        var contentCompare = contentCompareType.ToString();
        try
        {
            var assetsInfos = await FindAssetsAtReleasesAsync(moniker);
            if (assetsInfos.FirstOrDefault(m => m.Name.Contains(locale, StringComparison.OrdinalIgnoreCase) && m.Name.Contains(contentCompare, StringComparison.OrdinalIgnoreCase)) is not AssetsInfo targetAssetsInfo)
            {
                WriteMessageAndExit($"Not found {moniker}@{locale}@{contentCompareType} at github. Please build it yourself.");
                return;
            }
            var cacheUrlMd5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(targetAssetsInfo.DownloadUrl)));
            var cacheFile = Path.Combine(LocalizerEnvironment.OutputRoot, $"github_cache_{cacheUrlMd5}_{targetAssetsInfo.Id}_{targetAssetsInfo.Name}");

            //从缓存安装
            if (File.Exists(cacheFile))
            {
                Console.WriteLine($"Install form cache \"{cacheFile}\"");
                using var fileStream = File.OpenRead(cacheFile);
                using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, true);
                InstallFromZipArchive(target, moniker, locale, zipArchive, copyToNugetGlobalCache);
                return;
            }

            var data = await InstallFromUrlAsync(downloadUrl: targetAssetsInfo.DownloadUrl, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache, fileName: targetAssetsInfo.Name);

            if (data is not null
                && !File.Exists(cacheFile))
            {
                try
                {
                    File.WriteAllBytes(cacheFile, data);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Load from github fail. {ex.Message}.");
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

                return assets.Select(m => new AssetsInfo(m.GetProperty("name").GetString()!, m.GetProperty("browser_download_url").GetString()!, m.TryGetProperty("id", out var idNode) ? idNode.ToString() ?? string.Empty : string.Empty)).ToArray();
            }

            return Array.Empty<AssetsInfo>();

            bool IsLocalizedIntelliSenseFilePacksRelease(System.Text.Json.JsonElement jsonElement)
            {
                return jsonElement.GetProperty("name").GetString().EqualsOrdinalIgnoreCase($"{LocalizedIntelliSenseFilePacksReleaseName}-{moniker}");
            }
        }
    }

    private static async Task<byte[]?> InstallFromUrlAsync(string downloadUrl, string target, bool copyToNugetGlobalCache, string? fileName = null)
    {
        Console.WriteLine($"Start download {downloadUrl}");

        //直接内存处理，不缓存了
        using var httpRequestExecuteState = await downloadUrl.CreateHttpRequest().AutoRedirection().UseUserAgent(UserAgents.EdgeChromium).ExecuteAsync();

        using var responseMessage = httpRequestExecuteState.HttpResponseMessage;

        int? length = responseMessage.Content.Headers.TryGetValues(HttpHeaderDefinitions.ContentLength, out var lengths)
                      ? lengths.Count() == 1
                        ? int.TryParse(lengths.First(), out var lengthValue) ? lengthValue : null
                        : null
                      : null;

        var memeoryStream = new MemoryStream(length ?? 102400);
        using var responseStream = await responseMessage.Content.ReadAsStreamAsync();

        var buffer = new byte[102400];
        var lastDisplayTime = DateTime.UtcNow;
        var lastDisplayValue = 0.0;
        while (true)
        {
            var readCount = await responseStream.ReadAsync(buffer, 0, buffer.Length);
            if (readCount == 0)
            {
                break;
            }
            memeoryStream.Write(buffer, 0, readCount);

            if (length.HasValue
                && length > 0)
            {
                var value = memeoryStream.Length * 100.0 / length.Value;
                if (lastDisplayTime < DateTime.UtcNow.AddSeconds(-5)
                    || lastDisplayValue < value - 10)
                {
                    Console.WriteLine($"Download progress {value:F2}%");
                    lastDisplayTime = DateTime.UtcNow;
                    lastDisplayValue = value;
                }
            }
            else
            {
                if (lastDisplayTime < DateTime.UtcNow.AddSeconds(-5)
                    || lastDisplayValue < memeoryStream.Length - 512 * 1024)
                {
                    Console.WriteLine($"Download progress {memeoryStream.Length / 1024.0:F2}kb");
                    lastDisplayTime = DateTime.UtcNow;
                    lastDisplayValue = memeoryStream.Length;
                }
            }
        }

        Console.WriteLine($"Download completed.");

        if ((string.IsNullOrEmpty(fileName)
             && !TryGetFileName(responseMessage, out fileName))
            || !TryGetArchivePackageInfo(Path.GetFileNameWithoutExtension(fileName), out var moniker, out var locale, out var contentCompareType))
        {
            WriteMessageAndExit("can not get correct file name.");
            return null;
        }

        var data = memeoryStream.ToArray();
        using var zipArchive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read, true);

        InstallFromZipArchive(target, moniker, locale, zipArchive, copyToNugetGlobalCache);

        return data;

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

    private static void InstallFromZipArchive(string target, string moniker, string locale, ZipArchive zipArchive, bool copyToNugetGlobalCache)
    {
        var packRoot = DotNetEnvironmentUtil.GetSDKPackRoot(target);
        if (!Directory.Exists(packRoot))
        {
            WriteMessageAndExit($"not found any pack at the target SDK folder {target}. please check input.");
        }

        var applicationPackRefs = DotNetEnvironmentUtil.GetAllApplicationPacks(packRoot)
                                                       .SelectMany(m => m.Versions)
                                                       .SelectMany(m => m.Monikers)
                                                       .Where(m => m.Moniker.EqualsOrdinalIgnoreCase(moniker))
                                                       .SelectMany(m => m.Refs)
                                                       .Where(m => m.Culture is null)
                                                       .ToArray();

        var packEntryGroups = zipArchive.Entries.Where(m => m.Name.IsNotNullOrEmpty())
                                                .GroupBy(m => m.FullName.Contains('/') ? m.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries)[0] : m.FullName.Split('\\', StringSplitOptions.RemoveEmptyEntries)[0])    //路径分隔符可能为 / 或者 \
                                                .ToDictionary(m => m.Key, m => m.ToArray(), StringComparer.OrdinalIgnoreCase);

        var filesDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var count = 0;
        var nugetCount = 0;

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
                entry.ExtractToFile(targetFile, true);
                Console.WriteLine($"Created File: {targetFile}");

                if (copyToNugetGlobalCache)
                {
                    filesDictionary.TryAdd(Path.GetFileNameWithoutExtension(entry.Name), targetFile);
                }

                count++;
            }
        }

        if (copyToNugetGlobalCache)
        {
            var foundNugetPackageIntelliSenseFiles = GlobalPackagesFinder.EnumeratePackages()
                                                                         .Where(m => filesDictionary.ContainsKey(m.NormalizedName))
                                                                         .SelectMany(m => m.Versions)
                                                                         .SelectMany(m => m.Monikers)
                                                                         .Where(m => m.Moniker.EqualsOrdinalIgnoreCase(moniker))
                                                                         .SelectMany(m => m.IntelliSenseFiles)
                                                                         .Where(m => filesDictionary.ContainsKey(m.PackName))
                                                                         .ToArray();


            foreach (var nugetPackageIntelliSenseFile in foundNugetPackageIntelliSenseFiles)
            {
                var rootPath = Path.Combine(Path.GetDirectoryName(nugetPackageIntelliSenseFile.FilePath)!, locale);
                DirectoryUtil.CheckDirectory(rootPath);

                var sourceFile = filesDictionary[nugetPackageIntelliSenseFile.PackName];
                var targetFile = Path.Combine(rootPath, nugetPackageIntelliSenseFile.FileName);
                File.Copy(sourceFile, targetFile, true);

                Console.WriteLine($"Copy To Nuget Cache: {targetFile}");

                nugetCount++;
            }
        }

        Console.WriteLine($"Install done. {count} item copyed. {nugetCount} nuget item copyed.");
    }

    private static void InstallFromZipArchiveFile(string sourceFile, string target, bool copyToNugetGlobalCache)
    {
        try
        {
            if (!TryGetArchivePackageInfo(Path.GetFileNameWithoutExtension(sourceFile), out var moniker, out var locale, out var contentCompareType))
            {
                WriteMessageAndExit("The file name must be moniker@locale@ContentCompareType like net6.0@zh-cn@LocaleFirst.");
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
                WriteMessageAndExit($"open \"{sourceFile}\" fail. confirm the file is a valid archive file.");
                return;
            }
            InstallFromZipArchive(target, moniker, locale, zipArchive, copyToNugetGlobalCache);
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

    private record AssetsInfo(string Name, string DownloadUrl, string Id);
}
