using System.CommandLine;

using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildClearCommand()
    {
        var clearCommand = new Command("clear", Resources.StringCMDClearDescription);

        Argument<ClearType> argument = new("type");

        clearCommand.Add(argument);

        clearCommand.SetHandler<ClearType>(Clear, argument);

        return clearCommand;
    }

    private static void Clear(ClearType type)
    {
        try
        {
            switch (type)
            {
                case ClearType.All:
                    Directory.Delete(LocalizerEnvironment.CacheRoot, true);
                    Directory.Delete(LocalizerEnvironment.LogRoot, true);
                    Directory.Delete(LocalizerEnvironment.OutputRoot, true);
                    break;

                case ClearType.Cache:
                    Directory.Delete(LocalizerEnvironment.CacheRoot, true);
                    break;

                case ClearType.Output:
                    Directory.Delete(LocalizerEnvironment.OutputRoot, true);
                    break;

                case ClearType.Logs:
                    Directory.Delete(LocalizerEnvironment.LogRoot, true);
                    break;
            }
            Console.WriteLine($"[{type}] Cleared.");
        }
        finally
        {
            LocalizerEnvironment.CheckDirectories();
        }
    }

    private enum ClearType
    {
        All = 1,
        Cache = 1 << 1,
        Output = 1 << 2,
        Logs = 1 << 3,
    }
}
