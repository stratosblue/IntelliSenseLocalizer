using System.CommandLine;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

using Cuture.Http;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    public const string LocalizedIntelliSenseFilePacksReleaseName = "LocalizedIntelliSenseFilePacks";

    private static Command BuildLoadCommand()
    {
        var loadCommand = new Command("load", Resources.StringCMDLoadDescription);

        var sourceArgument = new Argument<string>("source", Resources.StringCMDLoadArgumentSourceDescription);
        var targetOption = new Option<string>(new[] { "-t", "--target" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDLoadOptionTargetDescription);

        {
            var githubLoadCommand = new Command("github", Resources.StringCMDLoadGithubDescription);

            var contentCompareTypeOption = new Option<ContentCompareType>(new[] { "-cc", "--content-compare" }, () => ContentCompareType.None, Resources.StringCMDBuildOptionContentCompareDescription);
            var localeOption = new Option<string>(new[] { "-l", "--locale" }, () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDBuildOptionLocaleDescription);

            githubLoadCommand.AddOption(targetOption);
            githubLoadCommand.AddOption(localeOption);
            githubLoadCommand.AddOption(contentCompareTypeOption);

            githubLoadCommand.SetHandler<string, string, ContentCompareType>(LoadFromGithub, targetOption, localeOption, contentCompareTypeOption);

            loadCommand.AddCommand(githubLoadCommand);
        }

        loadCommand.AddArgument(sourceArgument);
        loadCommand.AddOption(targetOption);

        loadCommand.SetHandler<string, string>(Load, sourceArgument, targetOption);

        return loadCommand;
    }

    private static void Load(string source, string target)
    {
        if (source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            LoadFromUrlAsync(source, target).Wait();
        }
        else
        {
            LoadFromFile(source, target);
        }
    }

    private static void LoadFromFile(string source, string target)
    {
        if (!File.Exists(source))
        {
            Console.WriteLine($"invalid source path \"{source}\". please input valid source path.");
            Environment.Exit(1);
            return;
        }

        using var stream = File.OpenRead(source);
        ZipArchive zipArchive;

        try
        {
            zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        }
        catch
        {
            Console.WriteLine($"open \"{source}\" fail. confirm the file is a valid archive file.");
            Environment.Exit(1);
            return;
        }

        LoadZipArchive(zipArchive, target);
    }

    private static void LoadFromGithub(string target, string locale, ContentCompareType contentCompareType)
    {
        locale = string.IsNullOrWhiteSpace(locale) ? LocalizerEnvironment.CurrentLocale : locale;
        CultureInfo cultureInfo;
        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(locale);
        }
        catch
        {
            s_logger.LogCritical("\"{locale}\" is not a effective locale.", locale);
            Environment.Exit(1);
            throw;
        }

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllInstalledApplicationPacks();
        var version = applicationPackDescriptors.Max(m => m.DotnetVersion)!.ToString(3);
        var contentCompare = contentCompareType.ToString();

        Console.WriteLine($"Trying load {version}@{locale} with ContentCompareType: {contentCompareType} from github.");

        LoadFromGithubAsync().Wait();

        async Task LoadFromGithubAsync()
        {
            try
            {
                var assetsInfos = await FindAssetsAtReleases(version);
                if (assetsInfos.FirstOrDefault(m => m.Name.Contains(locale, StringComparison.OrdinalIgnoreCase) && m.Name.Contains(contentCompare, StringComparison.OrdinalIgnoreCase)) is not AssetsInfo targetAssetsInfo)
                {
                    Console.WriteLine($"Not found {version}@{locale} with ContentCompareType: {contentCompareType} at github. Please build it yourself.");
                    Environment.Exit(1);
                    return;
                }
                await LoadFromUrlAsync(targetAssetsInfo.DownloadUrl, target);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load from github fail. {ex.Message}.");
                Environment.Exit(1);
                throw;
            }
        }

        static async Task<AssetsInfo[]> FindAssetsAtReleases(string version)
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
                return string.Equals(jsonElement.GetProperty("name").GetString(), $"{LocalizedIntelliSenseFilePacksReleaseName}-{version}", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static async Task<ZipArchive> LoadFromUrlAsync(string downloadUrl, string target)
    {
        Console.WriteLine($"Start download {downloadUrl}");

        //直接内存处理，不缓存了
        var data = await downloadUrl.CreateHttpRequest().AutoRedirection().UseUserAgent(UserAgents.EdgeChromium).GetAsBytesAsync();
        var zipArchive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read, true);

        LoadZipArchive(zipArchive, target);

        return zipArchive;
    }

    private static void LoadZipArchive(ZipArchive zipArchive, string target)
    {
        //文件路径正则判断，形如 *.App.Ref/6.0.3/ref/net6.0/zh-cn/*.xml
        var entryNameRegex = new Regex(@".+?\.App\.Ref[\/]\d+\.\d+\.\d+[\/]ref[\/]net\d+.*[\/][a-z]+-[a-z-]+[\/].+.xml$");

        try
        {
            foreach (var entry in zipArchive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }
                if (!entryNameRegex.IsMatch(entry.FullName))
                {
                    Console.WriteLine($"Load aborted. Archive file has invalid entry \"{entry.FullName}\".");
                    Environment.Exit(1);
                    return;
                }
            }

            zipArchive.ExtractToDirectory(target, true);
            Console.WriteLine($"Load completed. Archive file loaded into \"{target}\".");
        }
        finally
        {
            zipArchive.Dispose();
        }
    }

    private record AssetsInfo(string Name, string DownloadUrl);
}
