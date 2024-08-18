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
        var fileOption = new Option<string>(["-f", "--file"], Resources.StringCMDTranslateOptionFileDescription);
        var serverOption = new Option<string>(["-s", "--server"], Resources.StringCMDTranslateOptionServerDescription);
        var fromLocaleOption = new Option<string>(["-fl", "--from-locale"], () => "en-us", Resources.StringCMDTranslateOptionFromLocaleDescription);
        var targetLocalesOption = new Option<string>(["-tl", "--target-locales"], () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDTranslateOptionLocalesDescription);
        var contentCompareTypeOption = new Option<ContentCompareType>(["-cc", "--content-compare"], () => ContentCompareType.OriginFirst, Resources.StringCMDBuildOptionContentCompareDescription);
        var separateLineOption = new Option<string?>(["-sl", "--separate-line"], Resources.StringCMDBuildOptionSeparateLineDescription);
        var outputOption = new Option<string?>(["-o", "--output"], () => null, Resources.StringCMDTranslateOptionOutputDescription);
        var parallelCountOption = new Option<int>(["-pc", "--parallel-count"], () => 2, Resources.StringCMDBuildOptionParallelCountDescription);
        var patchOption = new Option<bool>(["-p", "--patch"], () => false, Resources.StringCMDTranslateOptionPatchDescription);

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

        translateCommand.SetHandler<string, string, string, string, ContentCompareType, string?, string?, int, bool, int?>(TranslateLocalizedIntelliSenseFile,
                                                                                                                           fileOption,
                                                                                                                           serverOption,
                                                                                                                           fromLocaleOption,
                                                                                                                           targetLocalesOption,
                                                                                                                           contentCompareTypeOption,
                                                                                                                           separateLineOption,
                                                                                                                           outputOption,
                                                                                                                           parallelCountOption,
                                                                                                                           patchOption,
                                                                                                                           s_logLevelOption);

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

    private static void TranslateLocalizedIntelliSenseFile(string file,
                                                           string server,
                                                           string fromLocale,
                                                           string targetLocalesString,
                                                           ContentCompareType contentCompareType,
                                                           string? separateLine,
                                                           string? outputRoot,
                                                           int parallelCount,
                                                           bool isPatch,
                                                           int? logLevel)
    {
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

        TranslateAsync().Wait();

        async Task TranslateAsync()
        {
            var translator = s_serviceProvider.GetRequiredService<LocalizeIntelliSenseTranslator>();

            foreach (var targetCultureInfo in targetCultureInfos)
            {
                var outputDirectory = Path.Combine(outputRoot!, targetCultureInfo.Name);
                DirectoryUtil.CheckDirectory(outputDirectory);

                var outputPath = Path.Combine(outputDirectory, Path.GetFileName(file));

                var context = new TranslateContext(file, contentCompareType, separateLine, outputPath, sourceCultureInfo, targetCultureInfo, contentTranslator, isPatch);

                await translator.TranslateAsync(context, default);
            }
        }
    }

    #endregion Private 方法
}
