using System.CommandLine;
using System.Globalization;

using IntelliSenseLocalizer.Properties;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    #region Private 方法

    private static Command BuildShowCommand()
    {
        var showCommand = new Command("show", Resources.StringCMDShowDescription);

        var filterOption = new Option<string>("--filter", "-f")
        {
            Description = Resources.StringOptionFilterDescription
        };

        {
            var showInstalledApplicationPacksCommand = new Command("packs", Resources.StringCMDShowPacksDescription)
            {
                filterOption
            };

            showInstalledApplicationPacksCommand.SetAction((parseResult =>
            {
                ShowInstalledApplicationPacks(parseResult.GetRequiredValue(filterOption));
            }));

            showCommand.Subcommands.Add(showInstalledApplicationPacksCommand);
        }

        {
            var packNameOption = new Option<string>("--pack", "-p")
            {
                Description = Resources.StringCMDShowOptionPackDescription
            };
            var monikerOption = new Option<string>("--moniker", "-m")
            {
                Description = Resources.StringCMDShowOptionMonikerDescription
            };

            var showInstalledApplicationPackRefsCommand = new Command("refs", Resources.StringCMDShowPackRefsDescription)
            {
                packNameOption,
                monikerOption,
                filterOption,
            };

            showInstalledApplicationPackRefsCommand.SetAction((parseResult =>
            {
                ShowInstalledApplicationPackRefs(packName: parseResult.GetRequiredValue(packNameOption),
                                                 moniker: parseResult.GetRequiredValue(monikerOption),
                                                 filterString: parseResult.GetRequiredValue(filterOption));
            }));

            showCommand.Subcommands.Add(showInstalledApplicationPackRefsCommand);
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

    #endregion Private 方法
}
