using System.CommandLine;
using System.Text.RegularExpressions;

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
            new Thread(_ =>
            {
                Console.WriteLine($"Program will exit at {waitSeconds} seconds later or press enter to exit.");
                Thread.Sleep(waitSeconds * 1000);
                Environment.Exit(0);
            })
            { IsBackground = true }.Start();
            Console.ReadLine();
        }

        return result;
    }

    #region Base

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

    private static void WriteMessageAndExit(string message)
    {
        Console.WriteLine(message);
        Environment.Exit(1);
    }

    #endregion Base
}
