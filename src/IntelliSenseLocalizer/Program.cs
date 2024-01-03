using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

using Cuture.Http;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static readonly LoggingLevelSwitch s_consoleLoggingLevelSwitch = new(LogEventLevel.Verbose);

    private static readonly Option<int?> s_logLevelOption = new(new[] { "-ll", "--log-level" }, Resources.StringOptionLogLevelDescription);

    private static Microsoft.Extensions.Logging.ILogger s_logger = null!;

    private static IServiceProvider s_serviceProvider = null!;

    private static int Main(string[] args)
    {
        if (TryCheckNewVersion(out var newVersion))
        {
            Console.WriteLine("-----------------------");
            var colorBackup = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Format(Resources.StringNewVersionFoundTip, newVersion.ToString()));
            Console.ForegroundColor = colorBackup;
            Console.WriteLine("-----------------------");
        }

        _ = TryGetNewVersionOnlineAsync();

        s_serviceProvider = BuildServiceProvider();
        s_logger = s_serviceProvider.GetRequiredService<ILogger<Program>>();

        var rootCommand = new RootCommand(Resources.StringRootCommandDescription)
        {
            BuildInstallCommand(),
            BuildUnInstallCommand(),
            BuildShowCommand(),
            BuildBuildCommand(),
            BuildClearCommand(),
            BuildCleanCommand(),
        };

        var customOption = new Option<string?>("--custom", "Custom addon options.");

        rootCommand.AddGlobalOption(s_logLevelOption);
        rootCommand.AddGlobalOption(customOption);

        var result = rootCommand.Invoke(args);

        var optionParseResult = customOption.Parse(args);

        // process like delay-exit-20s
        if (optionParseResult.CommandResult.GetValueForOption(customOption) is string customOptionString
            && Regex.Match(customOptionString, @"delay-exit-(\d+)s") is Match waitSecondsMatch
            && waitSecondsMatch.Groups.Count > 1
            && int.TryParse(waitSecondsMatch.Groups[1].Value, out var waitSeconds)
            && waitSeconds > 0)
        {
            DelayExitProcess(waitSeconds, 0);
        }

        return result;
    }

    #region Base

    [DoesNotReturn]
    private static void DelayExitProcess(int waitSeconds, int exitCode)
    {
        new Thread(_ =>
        {
            Console.WriteLine($"Program will exit at {waitSeconds} seconds later or press enter to exit.");
            Thread.Sleep(waitSeconds * 1000);
            Environment.Exit(exitCode);
        })
        { IsBackground = true }
        .Start();
        Console.ReadLine();
        Environment.Exit(exitCode);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}] {Message:lj}{NewLine}{Exception}", levelSwitch: s_consoleLoggingLevelSwitch)
                .WriteTo.File(Path.Combine(LocalizerEnvironment.LogRoot, "log.log"), rollingInterval: RollingInterval.Day, retainedFileTimeLimit: TimeSpan.FromDays(3))
                .CreateLogger();

            builder.AddSerilog(logger);
        });

        services.AddIntelliSenseLocalizer();

#if DEBUG
        return services.BuildServiceProvider(true);
#else
        return services.BuildServiceProvider();
#endif
    }

    private static Func<string, bool> BuildStringFilterFunc(string filterString)
    {
        return string.IsNullOrEmpty(filterString)
                    ? _ => true
                    : value => Regex.IsMatch(value, filterString, RegexOptions.IgnoreCase);
    }

    private static void SetLogLevel(int? logLevel)
    {
        if (logLevel is null)
        {
            s_consoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Information;
            return;
        }
        var level = logLevel >= (int)LogEventLevel.Verbose
                    && logLevel <= (int)LogEventLevel.Fatal
                        ? (LogEventLevel)logLevel
                        : LogEventLevel.Verbose;
        s_consoleLoggingLevelSwitch.MinimumLevel = level;
    }

    [DoesNotReturn]
    private static void WriteMessageAndExit(string message)
    {
        Console.WriteLine(message);
        DelayExitProcess(10, 1);
    }

    #endregion Base

    #region new version check

    private static readonly string s_newVersionCacheFilePath = Path.Combine(LocalizerEnvironment.CacheRoot, "new_version");

    private static bool TryGetCurrentVersion([NotNullWhen(true)] out Version? version)
    {
        var currentVersionString = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        if (currentVersionString is null
            || !Version.TryParse(currentVersionString.Split('-')[0], out version))
        {
            version = null;
            return false;
        }
        return true;
    }

    private static bool TryCheckNewVersion([NotNullWhen(true)] out Version? newVersion)
    {
        try
        {
            if (TryGetCurrentVersion(out var currentVersion)
                && File.Exists(s_newVersionCacheFilePath)
                && Version.TryParse(File.ReadAllText(s_newVersionCacheFilePath), out var onlineVersion)
                && onlineVersion > currentVersion)
            {
                newVersion = onlineVersion;
                return true;
            }
        }
        catch (Exception ex)
        {
            s_logger.LogDebug(ex, "check new version fail.");
        }
        newVersion = null;
        return false;
    }

    private static async Task TryGetNewVersionOnlineAsync()
    {
        try
        {
            if (!TryGetCurrentVersion(out var currentVersion))
            {
                return;
            }

            var nugetIndex = await "https://api.nuget.org/v3/index.json".CreateHttpRequest()
                                                                        .AutoRedirection(true)
                                                                        .GetAsDynamicJsonAsync();

            IEnumerable<dynamic> resources = nugetIndex!.resources;

            var searchQueryServiceInfo = resources.FirstOrDefault(m => string.Equals("SearchQueryService", m["@type"] as string));

            if (searchQueryServiceInfo is null)
            {
                return;
            }

            var searchQueryBaseUrl = searchQueryServiceInfo["@id"] as string;

            var searchQueryUrl = $"{searchQueryBaseUrl}?q=islocalizer&skip=0&take=10&prerelease=false&semVerLevel=2.0.0";

            var searchQueryResult = await searchQueryUrl.CreateHttpRequest()
                                                        .AutoRedirection(true)
                                                        .GetAsDynamicJsonAsync();

            if (searchQueryResult is null)
            {
                return;
            }

            IEnumerable<dynamic> searchQueryResultItems = searchQueryResult.data;

            var targetPacakgeInfo = searchQueryResultItems.FirstOrDefault(m => string.Equals("islocalizer", m.id as string));

            if (targetPacakgeInfo is null)
            {
                return;
            }

            IEnumerable<dynamic> versions = targetPacakgeInfo.versions;

            var newVersion = versions.Reverse().FirstOrDefault(m => Version.TryParse(m.version as string, out var version) && version > currentVersion);

            if (newVersion is null)
            {
                return;
            }

            await File.WriteAllTextAsync(s_newVersionCacheFilePath, newVersion.version as string);
        }
        catch (Exception ex)
        {
            s_logger.LogDebug(ex, "get new version online fail.");
        }
    }

    #endregion new version check
}
