using System.CommandLine;
using System.Globalization;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildBuildCommand()
    {
        var packNameOption = new Option<string>(new[] { "-p", "--pack" }, Resources.StringCMDBuildOptionPackDescription);
        var versionOption = new Option<string>(new[] { "-v", "--version" }, Resources.StringCMDBuildOptionVersionDescription);
        var localeOption = new Option<string>(new[] { "-l", "--locale" }, () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDBuildOptionLocaleDescription);
        var contentCompareTypeOption = new Option<ContentCompareType>(new[] { "-cc", "--content-compare" }, () => ContentCompareType.OriginFirst, Resources.StringCMDBuildOptionContentCompareDescription);
        var separateLineOption = new Option<string?>(new[] { "-sl", "--separate-line" }, Resources.StringCMDBuildOptionSeparateLineDescription);
        var outputOption = new Option<string>(new[] { "-o", "--output" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDBuildOptionOutputDescription);
        var parallelCountOption = new Option<int>(new[] { "-pc", "--parallel-count" }, () => 2, Resources.StringCMDBuildOptionParallelCountDescription);
        var nocacheOption = new Option<bool>(new[] { "-nc", "--no-cache" }, () => false, Resources.StringCMDBuildOptionNoCacheDescription);

        var buildCommand = new Command("build", Resources.StringCMDBuildDescription)
        {
            packNameOption,
            versionOption,
            localeOption,
            contentCompareTypeOption,
            separateLineOption,
            outputOption,
            parallelCountOption,
            nocacheOption,
        };

        buildCommand.SetHandler<string, string, string, ContentCompareType, string?, string, bool, int, int?>(BuildLocalizedIntelliSenseFile, packNameOption, versionOption, localeOption, contentCompareTypeOption, separateLineOption, outputOption, nocacheOption, parallelCountOption, s_logLevelOption);

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

        if (!Directory.Exists(outputRoot))
        {
            Directory.CreateDirectory(outputRoot);
        }

        var version = Version.TryParse(versionString, out var pv) ? pv : null;

        var packNameFilterFunc = BuildStringFilterFunc(packName);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllInstalledApplicationPacks();

        if (version is null)
        {
            version = applicationPackDescriptors.Max(m => m.DotnetVersion);
        }

        if (version is null || version.Major < 6)
        {
            s_logger.LogCritical("Not found the installed right version. or the input version is error.");
            Environment.Exit(1);
        }

        var packDescriptors = applicationPackDescriptors.Where(m => string.IsNullOrEmpty(packName) || string.Equals(m.Name, packName, StringComparison.OrdinalIgnoreCase))
                                                        .Where(m => m.DotnetVersion.Equals(version) && packNameFilterFunc(m.Name))
                                                        .ToArray();

        if (!packDescriptors.Any())
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

            int packCount = 0;
            foreach (var packDescriptor in packDescriptors)
            {
                packCount++;

                int packRefCount = 0;
                foreach (var packRefDescriptor in packDescriptor.PackRefs)
                {
                    packRefCount++;
                    
                    int intelliSenseFileCount = 0;
                    await Parallel.ForEachAsync(packRefDescriptor.IntelliSenseFiles, parallelOptions, async (intelliSenseFileDescriptor, cancellationToken) =>
                    {
                        var count = Interlocked.Increment(ref intelliSenseFileCount);
                        var outputPath = Path.Combine(outputRoot, packRefDescriptor.PackName, packRefDescriptor.PackVersion.ToString(3), "ref", packRefDescriptor.FrameworkMoniker, locale, intelliSenseFileDescriptor.FileName);

                        s_logger.LogInformation("Progress Pack[{packCount}/{packAll}]->PackRef[{packRefCount}/{packRefAll}]->File[{fileCount}/{fileAll}] - [{packName}:{version}:{name}]",
                                               packCount,
                                               packDescriptors.Length,
                                               packRefCount,
                                               packDescriptor.PackRefs.Count,
                                               count,
                                               packRefDescriptor.IntelliSenseFiles.Count,
                                               packRefDescriptor.PackName,
                                               packRefDescriptor.PackVersion,
                                               intelliSenseFileDescriptor.Name);

                        var context = new GenerateContext(intelliSenseFileDescriptor, contentCompareType, separateLine, outputPath, cultureInfo)
                        {
                            ParallelCount = parallelCount
                        };

                        await generator.GenerateAsync(context, default);
                    });
                }
            }
        }
    }
}
