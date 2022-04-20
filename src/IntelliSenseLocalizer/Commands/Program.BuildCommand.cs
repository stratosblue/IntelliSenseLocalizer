using System.CommandLine;
using System.Globalization;
using System.IO.Compression;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildBuildCommand()
    {
        var packNameOption = new Option<string>(new[] { "-p", "--pack" }, Resources.StringCMDBuildOptionPackDescription);
        var monikerOption = new Option<string>(new[] { "-m", "--moniker" }, Resources.StringCMDBuildOptionMonikerDescription);
        var localeOption = new Option<string>(new[] { "-l", "--locale" }, () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDBuildOptionLocaleDescription);
        var contentCompareTypeOption = new Option<ContentCompareType>(new[] { "-cc", "--content-compare" }, () => ContentCompareType.OriginFirst, Resources.StringCMDBuildOptionContentCompareDescription);
        var separateLineOption = new Option<string?>(new[] { "-sl", "--separate-line" }, Resources.StringCMDBuildOptionSeparateLineDescription);
        var outputOption = new Option<string>(new[] { "-o", "--output" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDBuildOptionOutputDescription);
        var parallelCountOption = new Option<int>(new[] { "-pc", "--parallel-count" }, () => 2, Resources.StringCMDBuildOptionParallelCountDescription);
        var nocacheOption = new Option<bool>(new[] { "-nc", "--no-cache" }, () => false, Resources.StringCMDBuildOptionNoCacheDescription);

        var buildCommand = new Command("build", Resources.StringCMDBuildDescription)
        {
            packNameOption,
            monikerOption,
            localeOption,
            contentCompareTypeOption,
            separateLineOption,
            outputOption,
            parallelCountOption,
            nocacheOption,
        };

        buildCommand.SetHandler<string, string, string, ContentCompareType, string?, string, bool, int, int?>(BuildLocalizedIntelliSenseFile, packNameOption, monikerOption, localeOption, contentCompareTypeOption, separateLineOption, outputOption, nocacheOption, parallelCountOption, s_logLevelOption);

        return buildCommand;
    }

    private static void BuildLocalizedIntelliSenseFile(string packName,
                                                       string versionString,
                                                       string locale,
                                                       ContentCompareType contentCompareType,
                                                       string? separateLine,
                                                       string outputRoot,
                                                       bool noCache,
                                                       int parallelCount,
                                                       int? logLevel)
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

        if (!string.IsNullOrEmpty(packName)
            && !packName.EndsWith(".Ref", StringComparison.OrdinalIgnoreCase))
        {
            packName = $"{packName}.Ref";
        }

        outputRoot = string.IsNullOrWhiteSpace(outputRoot) ? LocalizerEnvironment.OutputRoot : outputRoot;

        DirectoryUtil.CheckDirectory(outputRoot);

        var version = Version.TryParse(versionString, out var pv) ? pv : null;

        var packNameFilterFunc = BuildStringFilterFunc(packName);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllApplicationPacks();

        version ??= applicationPackDescriptors.SelectMany(m => m.Versions).Max(m => m.Version);

        if (version is null || version.Major < 6)
        {
            s_logger.LogCritical("Not found the installed right version. or the input version is error.");
            Environment.Exit(1);
        }

        var refDescriptors = applicationPackDescriptors.Where(m => string.IsNullOrEmpty(packName) || m.Name.EqualsOrdinalIgnoreCase(packName))
                                                       .SelectMany(m => m.Versions)
                                                       .Where(m => m.Version.Equals(version))
                                                       .SelectMany(m => m.Monikers)
                                                       .SelectMany(m => m.Refs)
                                                       .Where(m => m.Culture is null)
                                                       .ToArray();

        if (!refDescriptors.Any())
        {
            s_logger.LogCritical("Not found localizeable files.");
            Environment.Exit(1);
        }

        SetLogLevel(logLevel);

        GenerateAsync().Wait();

        async Task GenerateAsync()
        {
            var generator = s_serviceProvider.GetRequiredService<LocalizeIntelliSenseGenerator>();

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = parallelCount };

            //处理文件
            int refCount = 0;
            foreach (var refDescriptor in refDescriptors)
            {
                refCount++;
                var applicationPackRefMoniker = refDescriptor.OwnerMoniker;
                var moniker = applicationPackRefMoniker.Moniker;

                int intelliSenseFileCount = 0;
                await Parallel.ForEachAsync(refDescriptor.IntelliSenseFiles, parallelOptions, async (intelliSenseFileDescriptor, cancellationToken) =>
                {
                    var count = Interlocked.Increment(ref intelliSenseFileCount);

                    var applicationPackVersion = applicationPackRefMoniker.OwnerVersion;
                    var applicationPack = applicationPackVersion.OwnerPack;

                    var buildPath = Path.Combine(LocalizerEnvironment.BuildRoot, $"{moniker}@{locale}@{contentCompareType}", applicationPack.Name, intelliSenseFileDescriptor.FileName);

                    s_logger.LogInformation("Progress PackRef[{packRefCount}/{packRefAll}]->File[{fileCount}/{fileAll}]. Processing [{packName}:{version}:{name}] now.",
                                            refCount,
                                            refDescriptors.Length,
                                            count,
                                            refDescriptor.IntelliSenseFiles.Count,
                                            applicationPack.Name,
                                            refDescriptor.OwnerMoniker.OwnerVersion.Version,
                                            intelliSenseFileDescriptor.Name);

                    var context = new GenerateContext(intelliSenseFileDescriptor, contentCompareType, separateLine, buildPath, cultureInfo)
                    {
                        ParallelCount = parallelCount
                    };

                    await generator.GenerateAsync(context, default);
                });
            }

            //创建压缩文件
            var outputPackNames = refDescriptors.Select(m => $"{m.OwnerMoniker.Moniker}@{locale}@{contentCompareType}").ToHashSet();
            foreach (var outputPackName in outputPackNames)
            {
                var rootPath = Path.Combine(LocalizerEnvironment.BuildRoot, outputPackName);
                var tmpZipFileName = Path.Combine(rootPath, $"{Guid.NewGuid():n}.zip");
                using var fileStream = File.Open(tmpZipFileName, FileMode.Create, FileAccess.ReadWrite);
                using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create);
                foreach (var file in Directory.EnumerateFiles(rootPath, "*.xml", SearchOption.AllDirectories))
                {
                    var entryName = file.Replace(rootPath, string.Empty);
                    zipArchive.CreateEntryFromFile(file, entryName);
                }

                zipArchive.Dispose();
                fileStream.Dispose();
                var finalZipFilePath = Path.Combine(outputRoot, $"{outputPackName}.zip");
                File.Move(tmpZipFileName, finalZipFilePath, true);

                s_logger.LogWarning("localization pack is saved at {finalZipFilePath}.", finalZipFilePath);
            }
        }
    }
}
