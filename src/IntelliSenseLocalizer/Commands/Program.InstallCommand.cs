﻿using System.CommandLine;
using System.Diagnostics;

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
            if (OperatingSystem.IsWindows()
                && Environment.ProcessPath is string processPath
                && File.Exists(processPath))
            {
                //try run as administrator
                try
                {
                    var processStartInfo = new ProcessStartInfo(processPath, $"{Environment.CommandLine} --custom delay-exit-20s")
                    {
                        Verb = "runas",
                        UseShellExecute = true,
                    };

                    var process = Process.Start(processStartInfo);
                    if (process is not null)
                    {
                        return;
                    }
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine(innerEx.Message);
                }
            }
            else
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Please run as administrator again.");
            Environment.Exit(1);
        }
    }
}
