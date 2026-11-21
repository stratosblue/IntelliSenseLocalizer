using System.CommandLine;
using System.Globalization;
using IntelliSenseLocalizer.Properties;
using IntelliSenseLocalizer.ThirdParty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

    private static Command BuildTranslateCommand()
    {
        var fileOption = new Option<string>("--file", "-f")
        {
            Description = Resources.StringCMDTranslateOptionFileDescription,
        };
        var serverOption = new Option<string>("--server", "-s")
        {
            Description = Resources.StringCMDTranslateOptionServerDescription,
        };
        var fromLocaleOption = new Option<string>("--from-locale", "-fl")
        {
            Description = Resources.StringCMDTranslateOptionFromLocaleDescription,
            DefaultValueFactory = _ => "en-us",
        };
        var targetLocalesOption = new Option<string>("--target-locales", "-tl")
        {
            Description = Resources.StringCMDTranslateOptionLocalesDescription,
            DefaultValueFactory = _ => LocalizerEnvironment.CurrentLocale,
        };
        var contentCompareTypeOption = new Option<ContentCompareType>("--content-compare", "-cc")
        {
            Description = Resources.StringCMDBuildOptionContentCompareDescription,
            DefaultValueFactory = _ => ContentCompareType.OriginFirst,
        };
        var separateLineOption = new Option<string?>("--separate-line", "-sl")
        {
            Description = Resources.StringCMDBuildOptionSeparateLineDescription,
        };
        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = Resources.StringCMDTranslateOptionOutputDescription,
            DefaultValueFactory = _ => null,
        };
        var parallelCountOption = new Option<int>("--parallel-count", "-pc")
        {
            Description = Resources.StringCMDBuildOptionParallelCountDescription,
            DefaultValueFactory = _ => 1,
        };
        var patchOption = new Option<bool>("--patch", "-p")
        {
            Description = Resources.StringCMDTranslateOptionPatchDescription,
            DefaultValueFactory = _ => false,
        };

        var translateCommand = new Command("translate", Resources.StringCMDTranslateDescription)
        {
            fileOption,
            serverOption,
            fromLocaleOption,
            targetLocalesOption,
            contentCompareTypeOption,
            separateLineOption,
            outputOption,
            parallelCountOption,
            patchOption,
        };

        translateCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            await TranslateLocalizedIntelliSenseFile(file: parseResult.GetRequiredValue(fileOption),
                                                     server: parseResult.GetRequiredValue(serverOption),
                                                     fromLocale: parseResult.GetRequiredValue(fromLocaleOption),
                                                     targetLocalesString: parseResult.GetRequiredValue(targetLocalesOption),
                                                     contentCompareType: parseResult.GetRequiredValue(contentCompareTypeOption),
                                                     separateLine: parseResult.GetValue(separateLineOption),
                                                     outputRoot: parseResult.GetValue(outputOption),
                                                     parallelCount: parseResult.GetValue(parallelCountOption),
                                                     isPatch: parseResult.GetValue(patchOption),
                                                     logLevel: parseResult.GetValue(s_logLevelOption),
                                                     cancellationToken: cancellationToken);
        });

        return translateCommand;
    }

    private static CultureInfo GetCultureInfo(string locale)
    {
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

        return cultureInfo;
    }

    private static async Task TranslateLocalizedIntelliSenseFile(string file,
                                                           string server,
                                                           string fromLocale,
                                                           string targetLocalesString,
                                                           ContentCompareType contentCompareType,
                                                           string? separateLine,
                                                           string? outputRoot,
                                                           int parallelCount,
                                                           bool isPatch,
                                                           int? logLevel,
                                                           CancellationToken cancellationToken)
    {
        if (contentCompareType == ContentCompareType.Default)
        {
            contentCompareType = ContentCompareType.OriginFirst;
        }

        if (!File.Exists(file))
        {
            s_logger.LogCritical("xml file \"{File}\" not found.", file);
            Environment.Exit(1);
            return;
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            s_logger.LogCritical("\"server\" must be specified.");
            Environment.Exit(1);
            return;
        }

        if (string.IsNullOrWhiteSpace(fromLocale))
        {
            s_logger.LogCritical("\"from-locale\" must be specified.");
            Environment.Exit(1);
            return;
        }

        var sourceCultureInfo = GetCultureInfo(fromLocale);

        var targetLocales = string.IsNullOrWhiteSpace(targetLocalesString)
                            ? [LocalizerEnvironment.CurrentLocale]
                            : targetLocalesString.Split(';');

        if (targetLocales.Length == 0)
        {
            s_logger.LogCritical("\"target-locales\" must be specified.");
            Environment.Exit(1);
            return;
        }

        var targetCultureInfos = targetLocales.Select(GetCultureInfo).Distinct().ToArray();

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            outputRoot = Path.GetDirectoryName(file);
        }

        DirectoryUtil.CheckDirectory(outputRoot);

        s_logger.LogInformation("Start translate. Xml: {file}, Locale: {locale}, ContentCompareType: {ContentCompareType}.",
                                file,
                                targetLocales,
                                contentCompareType);

        SetLogLevel(logLevel);

        using var contentTranslator = new DefaultContentTranslator(server);

        await TranslateAsync(cancellationToken);

        async Task TranslateAsync(CancellationToken cancellationToken)
        {
            var translator = s_serviceProvider.GetRequiredService<LocalizeIntelliSenseTranslator>();

            foreach (var targetCultureInfo in targetCultureInfos)
            {
                var outputDirectory = Path.Combine(outputRoot!, targetCultureInfo.Name);
                DirectoryUtil.CheckDirectory(outputDirectory);

                var outputPath = Path.Combine(outputDirectory, Path.GetFileName(file));

                var context = new TranslateContext(file, contentCompareType, separateLine, outputPath, sourceCultureInfo, targetCultureInfo, contentTranslator, isPatch);

                await translator.TranslateAsync(context, cancellationToken);
            }
        }
    }

    #endregion Private 方法
}
