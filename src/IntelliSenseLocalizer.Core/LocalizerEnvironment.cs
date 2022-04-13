using System.Globalization;

namespace IntelliSenseLocalizer;

public static class LocalizerEnvironment
{
    public static string CacheRoot { get; }

    public static string CurrentLocale { get; }

    public static string DefaultSdkRoot { get; }

    public static string LogRoot { get; }

    public static string OutputRoot { get; }

    public static string WorkRootDirectory { get; }

    static LocalizerEnvironment()
    {
        WorkRootDirectory = Path.Combine(Path.GetTempPath(), "IntelliSenseLocalizer");
        LogRoot = Path.Combine(WorkRootDirectory, "logs");
        OutputRoot = Path.Combine(WorkRootDirectory, "output");
        CacheRoot = Path.Combine(WorkRootDirectory, "cache");

        CheckDirectories();

        CurrentLocale = CultureInfo.CurrentCulture.Name.ToLowerInvariant();

        DefaultSdkRoot = DotNetEnvironmentUtil.GetAllInstalledSDKPaths().FirstOrDefault() ?? string.Empty;
    }

    public static void CheckDirectories()
    {
        CheckDirectory(WorkRootDirectory);
        CheckDirectory(LogRoot);
        CheckDirectory(OutputRoot);
        CheckDirectory(CacheRoot);

        static void CheckDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}