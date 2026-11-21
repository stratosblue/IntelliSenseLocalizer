using System.CommandLine;
using System.Globalization;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

    private static Command BuildUnInstallCommand()
    {
        var uninstallCommand = new Command("uninstall", Resources.StringCMDUnInstallDescription);
        Argument<string> monikerArgument = new("moniker")
        {
            Description = Resources.StringCMDUnInstallArgumentMonikerDescription,
        };
        Argument<string> localeArgument = new("locale")
        {
            Description = Resources.StringCMDUnInstallArgumentLocaleDescription,
            DefaultValueFactory = _ => LocalizerEnvironment.CurrentLocale
        };
        Option<string> targetOption = new("--target", "-t")
        {
            Description = Resources.StringCMDUnInstallOptionTargetDescription,
            DefaultValueFactory = _ => LocalizerEnvironment.DefaultSdkRoot
        };

        uninstallCommand.Add(monikerArgument);
        uninstallCommand.Add(localeArgument);
        uninstallCommand.Add(targetOption);

        uninstallCommand.SetAction(parseResult =>
        {
            UnInstall(moniker: parseResult.GetRequiredValue(monikerArgument),
                      locale: parseResult.GetRequiredValue(localeArgument),
                      target: parseResult.GetRequiredValue(targetOption));
        });

        return uninstallCommand;
    }

    private static void UnInstall(string moniker, string locale, string target)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(locale);
        }
        catch
        {
            s_logger.LogCritical("\"{locale}\" is not a effective locale.", locale);
            Environment.Exit(1);
            throw;
        }

        var packRoot = DotNetEnvironmentUtil.GetSDKPackRoot(target);
        if (!Directory.Exists(packRoot))
        {
            WriteMessageAndExit($"not found any pack at the target SDK folder {target}. please check input.");
        }

        var allPack = DotNetEnvironmentUtil.GetAllApplicationPacks(packRoot)
                                           .SelectMany(m => m.Versions)
                                           .SelectMany(m => m.Monikers)
                                           .Where(m => m.Moniker.EqualsOrdinalIgnoreCase(moniker))
                                           .SelectMany(m => m.Refs)
                                           .Where(m => m.Culture == culture)
                                           .ToArray();

        var count = 0;
        try
        {
            foreach (var item in allPack.SelectMany(m => m.IntelliSenseFiles))
            {
                File.Delete(item.FilePath);
                Console.WriteLine(item.FilePath);
                count++;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            RunAsAdminUtil.TryReRunAsAdmin(ex);
            return;
        }
        Console.WriteLine($"UnInstall Done. {count} item deleted.");

        try
        {
            foreach (var packRefRoot in allPack.Select(m => m.RootPath).Distinct())
            {
                DeleteEmptyDirectory(packRefRoot, 4);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        static void DeleteEmptyDirectory(string? path, int count)
        {
            if (count > 0
                && !string.IsNullOrWhiteSpace(path)
                && Directory.GetDirectories(path).Length == 0
                && Directory.GetFiles(path).Length == 0)
            {
                Directory.Delete(path);

                DeleteEmptyDirectory(Path.GetDirectoryName(path), count - 1);
            }
        }
    }

    #endregion Private 方法
}
