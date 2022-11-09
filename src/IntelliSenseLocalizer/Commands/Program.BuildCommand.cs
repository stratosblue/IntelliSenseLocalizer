using System.CommandLine;
using System.Globalization;
using System.IO.Compression;

using IntelliSenseLocalizer.Models;
using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

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
                                                       string moniker,
                                                       string locale,
                                                       ContentCompareType contentCompareType,
                                                       string? separateLine,
                                                       string outputRoot,
                                                       bool noCache,
                                                       int parallelCount,
                                                       int? logLevel)
    {
        locale = string.IsNullOrWhiteSpace(locale) ? LocalizerEnvironment.CurrentLocale : locale;

        if (string.IsNullOrWhiteSpace("locale"))
        {
            s_logger.LogCritical("\"locale\" must be specified.");
            Environment.Exit(1);
            return;
        }

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

        var packNameFilterFunc = BuildStringFilterFunc(packName);
        var monikerFilterFunc = BuildStringFilterFunc(moniker);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllApplicationPacks().ToArray();

        var refDescriptors = applicationPackDescriptors.Where(m => packNameFilterFunc(m.Name))
                                                       .SelectMany(m => m.Versions)
                                                       .SelectMany(m => m.Monikers)
                                                       .Where(m => monikerFilterFunc(m.Moniker))
                                                       .GroupBy(m => m.Moniker)
                                                       .SelectMany(GetDistinctApplicationPackRefMonikerDescriptors)
                                                       .SelectMany(m => m.Refs)
                                                       .Where(m => m.Culture is null)
                                                       .ToArray();

        if (!refDescriptors.Any())
        {
            s_logger.LogCritical("Not found localizeable files.");
            Environment.Exit(1);
        }

        s_logger.LogInformation("Start generate. PackName: {PackName}, Moniker: {Moniker}, Locale: {locale}, ContentCompareType: {ContentCompareType}.",
                                packName,
                                moniker,
                                locale,
                                contentCompareType);

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

                s_logger.LogInformation("Processing pack [{PackName}:{Moniker}]. Progress {packRefCount}/{packRefAll}.",
                                        refDescriptor.OwnerMoniker.OwnerVersion.OwnerPack.Name,
                                        applicationPackRefMoniker.Moniker,
                                        refCount,
                                        refDescriptors.Length);

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
                DirectoryUtil.CheckDirectory(rootPath);

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

        static IEnumerable<ApplicationPackRefMonikerDescriptor> GetDistinctApplicationPackRefMonikerDescriptors(IEnumerable<ApplicationPackRefMonikerDescriptor> descriptors)
        {
            var dic = new Dictionary<string, ApplicationPackRefMonikerDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in descriptors)
            {
                dic[descriptor.OwnerVersion.OwnerPack.Name] = descriptor;
            }
            return dic.Values;
        }
    }

    #endregion Private 方法
}
