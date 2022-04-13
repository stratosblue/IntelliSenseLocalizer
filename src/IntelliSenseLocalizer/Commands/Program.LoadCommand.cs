using System.CommandLine;
using System.IO.Compression;
using System.Text.RegularExpressions;
using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildLoadCommand()
    {
        var loadCommand = new Command("load", Resources.StringCMDLoadDescription);

        var sourceArgument = new Argument<string>("source", Resources.StringCMDLoadArgumentSourceDescription);
        var targetOption = new Option<string>(new[] { "-t", "--target" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDLoadOptionTargetDescription);

        loadCommand.AddArgument(sourceArgument);
        loadCommand.AddOption(targetOption);

        loadCommand.SetHandler<string, string>(Load, sourceArgument, targetOption);

        return loadCommand;
    }

    private static void Load(string source, string target)
    {
        if (!File.Exists(source))
        {
            Console.WriteLine($"invalid source path \"{source}\". please input valid source path.");
            Environment.Exit(1);
            return;
        }

        using var stream = File.OpenRead(source);
        ZipArchive zipArchive;

        try
        {
            zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"open \"{source}\" fail. confirm the file is a valid archive file.");
            Environment.Exit(1);
            return;
        }

        //文件路径正则判断，形如 *.App.Ref/6.0.3/ref/net6.0/zh-cn/*.xml
        var entryNameRegex = new Regex(@".+?\.App\.Ref[\/]\d+\.\d+\.\d+[\/]ref[\/]net\d+.*[\/][a-z]+-[a-z-]+[\/].+.xml$");

        try
        {
            foreach (var entry in zipArchive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }
                if (!entryNameRegex.IsMatch(entry.FullName))
                {
                    Console.WriteLine($"Load aborted. Archive file has invalid entry \"{entry.FullName}\".");
                    Environment.Exit(1);
                    return;
                }
            }

            zipArchive.ExtractToDirectory(target, true);
            Console.WriteLine($"Load completed. Archive file loaded into \"{target}\".");
        }
        finally
        {
            zipArchive.Dispose();
        }
    }
}
