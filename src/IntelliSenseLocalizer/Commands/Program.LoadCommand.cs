using System.CommandLine;
using System.IO.Compression;
using System.Text.RegularExpressions;

using Cuture.Http;

using IntelliSenseLocalizer.Properties;

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

            githubLoadCommand.SetHandler(LoadFromGithub);

            loadCommand.AddCommand(githubLoadCommand);
        }

        loadCommand.AddArgument(sourceArgument);
        loadCommand.AddOption(targetOption);

        loadCommand.SetHandler<string, string>(Load, sourceArgument, targetOption);

        return loadCommand;
    }

    private static void Load(string source, string target)
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

    private static void LoadFromGithub()
    {
        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllInstalledApplicationPacks();
        var version = applicationPackDescriptors.Max(m => m.DotnetVersion)!.ToString(3);
        var locale = LocalizerEnvironment.CurrentLocale;
        Console.WriteLine($"Trying load {version}@{locale} from github.");

        LoadFromGithubAsync().Wait();

        async Task LoadFromGithubAsync()
        {
            try
            {
                var assetsInfos = await FindAssetsAtReleases();
                if (assetsInfos.FirstOrDefault(m => m.Name.Contains(version, StringComparison.OrdinalIgnoreCase) && m.Name.Contains(locale, StringComparison.OrdinalIgnoreCase)) is not AssetsInfo targetAssetsInfo)
                {
                    Console.WriteLine($"Not found {version} - {locale} at github. Please build it yourself.");
                    Environment.Exit(1);
                    return;
                }
                Console.WriteLine($"Start download {targetAssetsInfo.DownloadUrl}");

                //直接内存处理，不缓存了
                var data = await targetAssetsInfo.DownloadUrl.CreateHttpRequest().AutoRedirection().UseUserAgent(UserAgents.EdgeChromium).GetAsBytesAsync();

                using var zipArchive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read, true);

                LoadZipArchive(zipArchive, LocalizerEnvironment.OutputRoot);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load from github fail. {ex.Message}.");
                Environment.Exit(1);
            }
        }

        static async Task<AssetsInfo[]> FindAssetsAtReleases()
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

            static bool IsLocalizedIntelliSenseFilePacksRelease(System.Text.Json.JsonElement jsonElement)
            {
                return string.Equals(jsonElement.GetProperty("name").GetString(), LocalizedIntelliSenseFilePacksReleaseName, StringComparison.OrdinalIgnoreCase);
            }
        }
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
