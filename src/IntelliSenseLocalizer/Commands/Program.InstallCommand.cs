using System.CommandLine;
using System.Globalization;
using System.IO.Compression;

using Cuture.Http;

using IntelliSenseLocalizer.Nuget;
using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

    private static Command BuildInstallCommand()
    {
        var installCommand = new Command("install", Resources.StringCMDInstallDescription);
        var sourceOption = new Argument<string>("source", Resources.StringCMDInstallOptionSourceDescription);
        var targetOption = new Option<string>(["-t", "--target"], () => LocalizerEnvironment.DefaultSdkRoot, Resources.StringCMDInstallOptionTargetDescription);
        var copyToNugetGlobalCacheOption = new Option<bool>(["-ctn", "--copy-to-nuget-global-cache"], () => false, Resources.StringCMDInstallOptionCopyToNugetGlobalCacheDescription);

        installCommand.AddArgument(sourceOption);
        installCommand.AddOption(targetOption);
        installCommand.AddOption(copyToNugetGlobalCacheOption);

        installCommand.SetHandler((string source, string target, bool copyToNugetGlobalCache) =>
        {
            if (source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                InstallFromUrlWithRetryAsync(downloadUrl: source, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache).Wait();
            }
            else
            {
                InstallFromZipArchiveFile(sourceFile: source, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache);
            }
        }, sourceOption, targetOption, copyToNugetGlobalCacheOption);

        {
            var monikersOption = new Option<string>(["-m", "--moniker"], Resources.StringCMDInstallAutoOptionMonikerDescription);
            var localeOption = new Option<string>(["-l", "--locale"], () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDInstallOptionLocaleDescription);
            var contentCompareTypeOption = new Option<ContentCompareType>(["-cc", "--content-compare"], () => ContentCompareType.None, Resources.StringCMDBuildOptionContentCompareDescription);

            var autoInstallCommand = new Command("auto", Resources.StringCMDInstallAutoInstallDescription);
            autoInstallCommand.AddOption(targetOption);
            autoInstallCommand.AddOption(monikersOption);
            autoInstallCommand.AddOption(localeOption);
            autoInstallCommand.AddOption(contentCompareTypeOption);
            autoInstallCommand.AddOption(copyToNugetGlobalCacheOption);

            autoInstallCommand.SetHandler<string, string, string, ContentCompareType, bool>((string target, string monikers, string locale, ContentCompareType contentCompareType, bool copyToNugetGlobalCache) =>
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

                    if (string.IsNullOrWhiteSpace(monikers))
                    {
                        var maxVersion = applicationPacks.Max(m => m.Versions.Max(m => m.Version))!;
                        monikers = applicationPacks.SelectMany(m => m.Versions).Where(m => m.Version == maxVersion).First().Monikers.FirstOrDefault()?.Moniker ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(monikers))
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

                    foreach (var moniker in monikers.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        InstallFromNugetAsync(target, moniker, locale, contentCompareType, copyToNugetGlobalCache).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auto install failed: {ex.InnerException ?? ex}");
                    Console.WriteLine("press any key to continue");
                    Console.ReadKey();
                }
            }, targetOption, monikersOption, localeOption, contentCompareTypeOption, copyToNugetGlobalCacheOption);

            installCommand.Add(autoInstallCommand);
        }

        return installCommand;
    }

    #endregion Private 方法

    #region nuget.org

    private static async Task InstallFromNugetAsync(string target, string moniker, string locale, ContentCompareType contentCompareType, bool copyToNugetGlobalCache)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);

            s_logger.LogInformation("getting nuget index.");

            var nugetIndex = await "https://api.nuget.org/v3/index.json".CreateHttpRequest()
                                                                        .AutoRedirection(true)
                                                                        .GetAsDynamicJsonAsync();

            IEnumerable<dynamic> resources = nugetIndex!.resources;

            var searchQueryServiceInfo = resources.First(m => string.Equals("SearchQueryService", m["@type"] as string));

            var searchQueryBaseUrl = searchQueryServiceInfo["@id"] as string;

            s_logger.LogInformation("nuget search query service address {searchQueryBaseUrl}.", searchQueryBaseUrl);

            var searchQueryUrl = $"{searchQueryBaseUrl}?q={NugetPackageName}&skip=0&take=100&prerelease=true&semVerLevel=2.0.0";

            s_logger.LogInformation("querying language pack package.");

            var searchQueryResult = await searchQueryUrl.CreateHttpRequest()
                                                        .AutoRedirection(true)
                                                        .GetAsDynamicJsonAsync();

            IEnumerable<dynamic> searchQueryResultItems = searchQueryResult!.data;

            var targetPacakgeInfo = searchQueryResultItems.FirstOrDefault(m => string.Equals(NugetPackageName, m.id as string));

            if (targetPacakgeInfo is null)
            {
                WriteMessageAndExit("query language pack fail.");
            }

            IEnumerable<dynamic> versions = targetPacakgeInfo.versions;

            var targetVersionInfo = versions.Reverse().FirstOrDefault(m =>
            {
                try
                {
                    var versionString = (string)m.version;
                    var langPackVersion = LangPackVersion.Decode(versionString.Replace(NugetPackageName, string.Empty).TrimStart('.'));
                    return contentCompareType == langPackVersion.ContentCompareType
                           && langPackVersion.Moniker.EqualsOrdinalIgnoreCase(moniker)
                           && IsAdaptCulture(langPackVersion.Culture, culture);
                }
                catch { }
                return false;
            });

            if (targetVersionInfo is null)
            {
                WriteMessageAndExit($"not found package at nuget.org for [{target} - {moniker} - {locale} - {contentCompareType}]");
            }

            s_logger.LogInformation("getting language pack package detail.");

            var packgetDetailUrl = (string)targetVersionInfo["@id"];
            var packageDetailInfo = await packgetDetailUrl.CreateHttpRequest()
                                                          .AutoRedirection(true)
                                                          .GetAsDynamicJsonAsync();

            var packageDownloadUrl = (string)packageDetailInfo!.packageContent;

            var cacheFile = Path.Combine(LocalizerEnvironment.OutputRoot, Path.GetFileName(packageDownloadUrl));

            //从缓存安装
            if (File.Exists(cacheFile))
            {
                Console.WriteLine($"Install form cache \"{cacheFile}\"");
                using var fileStream = File.OpenRead(cacheFile);
                using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, true);
                InstallFromZipArchive(target, zipArchive, copyToNugetGlobalCache);
                return;
            }

            var data = await InstallFromUrlWithRetryAsync(downloadUrl: packageDownloadUrl, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache);

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
            Console.WriteLine($"Load from nuget.org fail. {ex.Message}.");
            throw;
        }
    }

    private static bool IsAdaptCulture(CultureInfo? culture, CultureInfo targetCulture)
    {
        if (culture == null)
        {
            return false;
        }
        if (culture.Equals(CultureInfo.InvariantCulture))
        {
            return false;
        }
        if (targetCulture.Equals(culture))
        {
            return true;
        }
        return IsAdaptCulture(culture.Parent, targetCulture);
    }

    #endregion nuget.org

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

        var data = memeoryStream.ToArray();
        using var zipArchive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read, true);

        InstallFromZipArchive(target, zipArchive, copyToNugetGlobalCache);

        return data;
    }

    private static async Task<byte[]?> InstallFromUrlWithRetryAsync(string downloadUrl, string target, bool copyToNugetGlobalCache, int retryCount = 3)
    {
        do
        {
            retryCount--;
            try
            {
                return await InstallFromUrlAsync(downloadUrl: downloadUrl, target: target, copyToNugetGlobalCache: copyToNugetGlobalCache);
            }
            catch (Exception ex)
            {
                if (retryCount > 0)
                {
                    Console.WriteLine($"Download fail {ex}");
                    Console.WriteLine("Retry download");
                }
                else
                {
                    throw;
                }
            }
        } while (retryCount > 0);

        throw new InvalidOperationException("Download error");
    }

    private static void InstallFromZipArchive(string target, ZipArchive zipArchive, bool copyToNugetGlobalCache)
    {
        var manifestEntry = zipArchive.GetEntry(LanguagePackManifest.ManifestFileName);

        if (manifestEntry is null)
        {
            WriteMessageAndExit($"not found \"{LanguagePackManifest.ManifestFileName}\" in zipArchive.");
        }

        var contentEntries = zipArchive.Entries.Where(m => m.FullName.StartsWith("content/")).ToList();

        if (contentEntries.Count == 0)
        {
            WriteMessageAndExit($"not found intellisense files in zipArchive.");
        }

        using var manifestStream = manifestEntry.Open();
        using var manifestStreamReader = new StreamReader(manifestStream);
        var manifestJson = manifestStreamReader.ReadToEnd();

        var languagePackManifest = LanguagePackManifest.FromJson(manifestJson);

        var locale = languagePackManifest.Culture.Name.ToLowerInvariant();
        var moniker = languagePackManifest.Moniker;

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

        var packEntryGroups = contentEntries.Where(m => m.Name.IsNotNullOrEmpty())
                                            .GroupBy(m => m.FullName.Contains('/') ? m.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries)[1] : m.FullName.Split('\\', StringSplitOptions.RemoveEmptyEntries)[1])    //路径分隔符可能为 / 或者 \
                                            .ToDictionary(m => m.Key, m => m.ToArray(), StringComparer.OrdinalIgnoreCase);

        var filesDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var count = 0;
        var nugetCount = 0;

        var nugetGlobalPackages = GlobalPackagesFinder.EnumeratePackages().ToList();

        foreach (var applicationPackRef in applicationPackRefs)
        {
            var versionDescriptor = applicationPackRef.OwnerMoniker.OwnerVersion;
            var version = versionDescriptor.Version;
            var packName = versionDescriptor.OwnerPack.Name;
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

            var nugetRefCaches = nugetGlobalPackages.Where(m => string.Equals(packName, m.NormalizedName, StringComparison.OrdinalIgnoreCase))
                                                    .SelectMany(m => m.Versions)
                                                    .Where(m => m.Version.Major == version.Major && m.Version.Minor == version.Minor)
                                                    .ToList();

            foreach (var nugetRefCache in nugetRefCaches)
            {
                rootPath = Path.Combine(nugetRefCache.RootPath, "ref", moniker, locale);

                DirectoryUtil.CheckDirectory(rootPath);

                foreach (var entry in entries)
                {
                    var targetFile = Path.Combine(rootPath, entry.Name);
                    entry.ExtractToFile(targetFile, true);
                    Console.WriteLine($"Created File: {targetFile}");

                    nugetCount++;
                }
            }
        }

        if (copyToNugetGlobalCache)
        {
            var foundNugetPackageIntelliSenseFiles = nugetGlobalPackages.Where(m => filesDictionary.ContainsKey(m.NormalizedName))
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
            InstallFromZipArchive(target, zipArchive, copyToNugetGlobalCache);
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
}
