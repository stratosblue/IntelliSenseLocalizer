using System.CommandLine;
using System.Globalization;

using IntelliSenseLocalizer.Models;
using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

    private static Command BuildCleanCommand()
    {
        var cleanCommand = new Command("clean", Resources.StringCMDCleanDescription);

        cleanCommand.SetHandler(Clean);

        return cleanCommand;
    }

    private static void Clean()
    {
        try
        {
            var shouldDeletePacks = DotNetEnvironmentUtil.GetAllApplicationPacks()
                                                         .SelectMany(m => m.Versions)
                                                         .Where(m => IsLocaleRefOnly(m))
                                                         .Select(m => m.RootPath)
                                                         .ToArray();
            if (shouldDeletePacks.Length == 0)
            {
                Console.WriteLine("No folder can delete.");
                return;
            }
            Console.WriteLine($"Delete this folders? {Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, shouldDeletePacks)}{Environment.NewLine}{Environment.NewLine}(input y to delete)");
            var ensureInput = Console.ReadLine()?.Trim();
            if (!string.Equals("y", ensureInput, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            foreach (var item in shouldDeletePacks)
            {
                Directory.Delete(item, true);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            RunAsAdminUtil.TryReRunAsAdmin(ex);
            return;
        }
    }

    private static bool IsLocaleRefOnly(ApplicationPackVersionDescriptor versionDescriptor)
    {
        if (!NoFiles(versionDescriptor.RootPath))
        {
            return false;
        }

        if (SingleTargetSubDir(versionDescriptor.RootPath, "ref", out var refDirPath)
            && (NoFiles(refDirPath)
                && Directory.GetDirectories(refDirPath) is string[] refDirSubDirs
                && refDirSubDirs.Length == 1
                && refDirSubDirs[0] is string monikerDir)
            && versionDescriptor.Monikers.Any(m => m.Moniker.EqualsOrdinalIgnoreCase(Path.GetFileName(monikerDir)))
            && NoFiles(monikerDir)
            && Directory.GetDirectories(monikerDir).All(IsCultureDir)
            && Directory.GetDirectories(monikerDir).All(XmlFilesOnly))
        {
            return true;
        }
        return false;

        static bool NoFiles(string dir) => !Directory.EnumerateFiles(dir).Any();

        static bool NoDirs(string dir) => !Directory.EnumerateDirectories(dir).Any();

        static bool XmlFilesOnly(string dir) => NoDirs(dir) && Directory.GetFiles(dir).All(m => Path.GetExtension(m).EqualsOrdinalIgnoreCase(".xml"));

        static bool IsCultureDir(string dir)
        {
            try
            {
                CultureInfo.GetCultureInfo(Path.GetFileName(dir));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool SingleTargetSubDir(string root, string targetDirName, out string targetDirFullPath)
        {
            var dirs = Directory.GetDirectories(root);
            if (dirs.Length == 1
                && targetDirName.EqualsOrdinalIgnoreCase(Path.GetFileName(dirs[0])))
            {
                targetDirFullPath = dirs[0];
                return true;
            }

            targetDirFullPath = string.Empty;
            return false;
        }
    }

    #endregion Private 方法
}
