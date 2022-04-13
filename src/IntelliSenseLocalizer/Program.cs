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
            BuildLoadCommand(),
            BuildClearCommand(),
        };

        rootCommand.AddGlobalOption(s_logLevelOption);

        return rootCommand.Invoke(args);
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

    #endregion Base
}
