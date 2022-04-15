using System.CommandLine;
using System.Globalization;

using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildUnInstallCommand()
    {
        var uninstallCommand = new Command("uninstall", Resources.StringCMDUnInstallDescription);
        Argument<string> monikerArgument = new("moniker", Resources.StringCMDUnInstallArgumentMonikerDescription);
        Argument<string> localeArgument = new("locale", () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDUnInstallArgumentLocaleDescription);
        Option<string> targetOption = new(new[] { "-t", "--target" }, () => LocalizerEnvironment.DefaultSdkRoot, Resources.StringCMDUnInstallOptionTargetDescription);

        uninstallCommand.Add(monikerArgument);
        uninstallCommand.Add(localeArgument);
        uninstallCommand.Add(targetOption);

        uninstallCommand.SetHandler<string, string, string>(UnInstall, monikerArgument, localeArgument, targetOption);

        return uninstallCommand;
    }

    private static void UnInstall(string moniker, string locale, string target)
    {
        try
        {
            CultureInfo.GetCultureInfo(locale);
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
            Console.WriteLine($"not found any pack at the target SDK folder {target}. please check input.");
            Environment.Exit(1);
        }

        var allLocalizedPack = DotNetEnvironmentUtil.GetLocalizedApplicationPacks(packRoot)
                                                    .Where(m => string.Equals(m.Locale, locale, StringComparison.OrdinalIgnoreCase))
                                                    .Select(m => m.Descriptor)
                                                    .SelectMany(m => m.PackRefs)
                                                    .Where(m => m.FrameworkMoniker == moniker);

        var count = 0;
        try
        {
            foreach (var item in allLocalizedPack.SelectMany(m => m.IntelliSenseFiles))
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
    }
}
