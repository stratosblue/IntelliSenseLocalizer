using System.Diagnostics;

namespace IntelliSenseLocalizer;

internal static class RunAsAdminUtil
{
    public static void TryReRunAsAdmin(Exception exception)
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
            Console.WriteLine(exception.Message);
        }

        Console.WriteLine("Please run as administrator again.");
        Environment.Exit(1);
    }
}
