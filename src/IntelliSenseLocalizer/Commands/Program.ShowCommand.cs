using System.CommandLine;

using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private static Command BuildShowCommand()
    {
        var showCommand = new Command("show", Resources.StringCMDShowDescription);

        var filterOption = new Option<string>(new[] { "-f", "--filter" }, Resources.StringOptionFilterDescription);

        {
            var showInstalledApplicationPacksCommand = new Command("packs", Resources.StringCMDShowPacksDescription)
            {
                filterOption
            };

            showInstalledApplicationPacksCommand.SetHandler<string>(ShowInstalledApplicationPacks, filterOption);

            showCommand.AddCommand(showInstalledApplicationPacksCommand);
        }

        {
            var packNameOption = new Option<string>(new[] { "-p", "--pack" }, Resources.StringCMDShowOptionPackDescription);
            var versionOption = new Option<string>(new[] { "-v", "--version" }, Resources.StringCMDShowOptionVersionDescription);

            var showInstalledApplicationPackRefsCommand = new Command("refs", Resources.StringCMDShowPackRefsDescription)
            {
                packNameOption,
                versionOption,
                filterOption,
            };

            showInstalledApplicationPackRefsCommand.SetHandler<string, string, string>(ShowInstalledApplicationPackRefs, packNameOption, versionOption, filterOption);

            showCommand.AddCommand(showInstalledApplicationPackRefsCommand);
        }

        return showCommand;
    }

    private static void ShowInstalledApplicationPackRefs(string packName, string versionString, string filterString)
    {
        var packNameFilterFunc = BuildStringFilterFunc(packName);
        var filterFunc = BuildStringFilterFunc(filterString);

        var version = Version.TryParse(versionString, out var pv) ? pv : null;

        if (version is null && !string.IsNullOrWhiteSpace(versionString))
        {
            Console.WriteLine("input version value is error format.");
            Environment.Exit(1);
        }
        
        if (!packName.EndsWith(".Ref", StringComparison.OrdinalIgnoreCase))
        {
            packName = $"{packName}.Ref";
        }

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllInstalledApplicationPacks();

        var query = applicationPackDescriptors.Where(m => string.IsNullOrEmpty(packName) || string.Equals(m.Name, packName, StringComparison.OrdinalIgnoreCase))
                                              .Where(m => version is null || m.DotnetVersion.Equals(version))
                                              .SelectMany(m => m.PackRefs)
                                              .SelectMany(m => m.IntelliSenseFiles)
                                              .Where(m => filterFunc(m.Name));

        foreach (var intelliSenseFileDescriptor in query)
        {
            Console.WriteLine($"[{intelliSenseFileDescriptor.Name}] at [{intelliSenseFileDescriptor.FilePath}]");
        }
    }

    private static void ShowInstalledApplicationPacks(string filterString)
    {
        var filterFunc = BuildStringFilterFunc(filterString);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllInstalledApplicationPacks().ToArray();
        foreach (var packDescriptor in applicationPackDescriptors.Where(m => filterFunc(m.Name)))
        {
            Console.WriteLine(packDescriptor);
        }
    }
}
