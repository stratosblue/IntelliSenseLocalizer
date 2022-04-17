using System.CommandLine;

using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildInstallCommand()
    {
        var installCommand = new Command("install", Resources.StringCMDInstallDescription);
        var sourceOption = new Option<string>(new[] { "-s", "--source" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDInstallOptionSourceDescription);
        var targetOption = new Option<string>(new[] { "-t", "--target" }, () => LocalizerEnvironment.DefaultSdkRoot, Resources.StringCMDInstallOptionTargetDescription);
        var versionOption = new Option<string>(new[] { "-v", "--version" }, Resources.StringCMDInstallOptionVersionDescription);
        var localeOption = new Option<string>(new[] { "-l", "--locale" }, Resources.StringCMDInstallOptionLocaleDescription);

        installCommand.AddOption(sourceOption);
        installCommand.AddOption(targetOption);
        installCommand.AddOption(versionOption);
        installCommand.AddOption(localeOption);

        installCommand.SetHandler<string, string, string, string>(Install, sourceOption, targetOption, versionOption, localeOption);

        {
            var autoInstallCommand = new Command("auto", Resources.StringCMDInstallAutoInstallDescription);

            autoInstallCommand.SetHandler(() =>
            {
                var writeTestPath = Path.Combine(LocalizerEnvironment.DefaultSdkRoot, ".write_test");
                try
                {
                    File.WriteAllText(writeTestPath, "write_test");
                    File.Delete(writeTestPath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    RunAsAdminUtil.TryReRunAsAdmin(ex);
                    return;
                }
                
                LoadFromGithub(LocalizerEnvironment.OutputRoot, LocalizerEnvironment.CurrentLocale, ContentCompareType.Default);
                Install(LocalizerEnvironment.OutputRoot, LocalizerEnvironment.DefaultSdkRoot, string.Empty, LocalizerEnvironment.CurrentLocale);
            });

            installCommand.Add(autoInstallCommand);
        }

        return installCommand;
    }

    private static void Install(string source, string target, string versionString, string locale)
    {
        var packRoot = DotNetEnvironmentUtil.GetSDKPackRoot(target);
        if (!Directory.Exists(packRoot))
        {
            Console.WriteLine($"not found any pack at the target SDK folder {target}. please check input.");
            Environment.Exit(1);
        }
        var count = 0;
        try
        {
            bool FileFilter(string src) => (string.IsNullOrWhiteSpace(versionString) || src.Contains(versionString, StringComparison.OrdinalIgnoreCase))
                                           && (string.IsNullOrWhiteSpace(locale) || src.Contains(locale, StringComparison.OrdinalIgnoreCase));

            foreach (var sourceRefDirectory in Directory.EnumerateDirectories(source, "*.Ref", SearchOption.TopDirectoryOnly))
            {
                var targetPackDirectory = Path.Combine(packRoot, Path.GetFileName(sourceRefDirectory));
                foreach (var (From, To) in FileCopyUtil.CopyDirectory(sourceRefDirectory, targetPackDirectory, "*.xml", FileFilter, true, true))
                {
                    count++;
                    Console.WriteLine($"{From.Substring(source.Length)} -> {To}");
                }
            }

            Console.WriteLine($"Install done. {count} item copyed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            RunAsAdminUtil.TryReRunAsAdmin(ex);
            return;
        }
    }
}
