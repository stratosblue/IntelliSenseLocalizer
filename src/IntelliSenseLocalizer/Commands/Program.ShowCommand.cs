using System.CommandLine;
using System.Globalization;

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
            var monikerOption = new Option<string>(new[] { "-m", "--moniker" }, Resources.StringCMDShowOptionMonikerDescription);

            var showInstalledApplicationPackRefsCommand = new Command("refs", Resources.StringCMDShowPackRefsDescription)
            {
                packNameOption,
                monikerOption,
                filterOption,
            };

            showInstalledApplicationPackRefsCommand.SetHandler<string, string, string>(ShowInstalledApplicationPackRefs, packNameOption, monikerOption, filterOption);

            showCommand.AddCommand(showInstalledApplicationPackRefsCommand);
        }

        return showCommand;
    }

    private static void ShowInstalledApplicationPackRefs(string packName, string moniker, string filterString)
    {
        var packNameFilterFunc = BuildStringFilterFunc(packName);
        var monikerFilterFunc = BuildStringFilterFunc(moniker);

        var filterFunc = BuildStringFilterFunc(filterString);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllApplicationPacks();

        var query = applicationPackDescriptors.Where(m => packNameFilterFunc(m.Name))
                                              .SelectMany(m => m.Versions)
                                              .SelectMany(m => m.Monikers)
                                              .Where(m => monikerFilterFunc(m.Moniker))
                                              .SelectMany(m => m.Refs)
                                              .SelectMany(m => m.IntelliSenseFiles)
                                              .Where(m => filterFunc(m.Name))
                                              .OrderBy(m => m.Name);

        foreach (var intelliSenseFileDescriptor in query)
        {
            if (intelliSenseFileDescriptor.Culture is CultureInfo culture)
            {
                Console.WriteLine($"[{intelliSenseFileDescriptor.Name}]({culture}) at [{intelliSenseFileDescriptor.FilePath}]");
            }
            else
            {
                Console.WriteLine($"[{intelliSenseFileDescriptor.Name}] at [{intelliSenseFileDescriptor.FilePath}]");
            }
        }
    }

    private static void ShowInstalledApplicationPacks(string filterString)
    {
        var filterFunc = BuildStringFilterFunc(filterString);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllApplicationPacks().ToArray();
        foreach (var packDescriptor in applicationPackDescriptors.Where(m => filterFunc(m.Name)))
        {
            Console.WriteLine(packDescriptor);
        }
    }
}
