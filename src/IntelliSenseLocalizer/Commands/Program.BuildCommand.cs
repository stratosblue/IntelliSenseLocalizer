using System.CommandLine;
using System.Diagnostics;
using System.Globalization;

using IntelliSenseLocalizer.Models;
using IntelliSenseLocalizer.Properties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

internal partial class Program
{
    private const string PackCsprojFileName = "pack.csproj";

    private const string NugetPackageName = "IntelliSenseLocalizer.LanguagePack";

    private const string PackCsprojContent = $@" <Project Sdk=""Microsoft.NET.Sdk"">

	<PropertyGroup>
		<TargetFramework>netstandard2.0</TargetFramework>
		<IncludeBuildOutput>false</IncludeBuildOutput>
	</PropertyGroup>

	<ItemGroup>
		<None Include="".\**\*.xml"" Pack=""True"" PackagePath=""content"" />
	</ItemGroup>

	<ItemGroup>
	  <None Update=""islocalizer.manifest.json"" Pack=""True"" PackagePath=""/"" />
	</ItemGroup>

	<!--Package Info-->
	<PropertyGroup>
		<Description>Localized IntelliSense files pack. 本地化IntelliSense文件包。</Description>

		<Authors>stratos</Authors>
		<PackageLicenseExpression>MIT</PackageLicenseExpression>
		<PackageProjectUrl>https://github.com/stratosblue/intellisenselocalizer</PackageProjectUrl>

		<RepositoryType>git</RepositoryType>
		<RepositoryUrl>$(PackageProjectUrl)</RepositoryUrl>

		<PackageTags>localized-intellisense-files intellisense-files localization-files</PackageTags>
	</PropertyGroup>
</Project>
";

    #region Private 方法

    private static Command BuildBuildCommand()
    {
        var packNameOption = new Option<string>(new[] { "-p", "--pack" }, Resources.StringCMDBuildOptionPackDescription);
        var monikerOption = new Option<string>(new[] { "-m", "--moniker" }, Resources.StringCMDBuildOptionMonikerDescription);
        var localeOption = new Option<string>(new[] { "-l", "--locale" }, () => LocalizerEnvironment.CurrentLocale, Resources.StringCMDBuildOptionLocaleDescription);
        var contentCompareTypeOption = new Option<ContentCompareType>(new[] { "-cc", "--content-compare" }, () => ContentCompareType.OriginFirst, Resources.StringCMDBuildOptionContentCompareDescription);
        var separateLineOption = new Option<string?>(new[] { "-sl", "--separate-line" }, Resources.StringCMDBuildOptionSeparateLineDescription);
        var outputOption = new Option<string>(new[] { "-o", "--output" }, () => LocalizerEnvironment.OutputRoot, Resources.StringCMDBuildOptionOutputDescription);
        var parallelCountOption = new Option<int>(new[] { "-pc", "--parallel-count" }, () => 2, Resources.StringCMDBuildOptionParallelCountDescription);
        var nocacheOption = new Option<bool>(new[] { "-nc", "--no-cache" }, () => false, Resources.StringCMDBuildOptionNoCacheDescription);

        var buildCommand = new Command("build", Resources.StringCMDBuildDescription)
        {
            packNameOption,
            monikerOption,
            localeOption,
            contentCompareTypeOption,
            separateLineOption,
            outputOption,
            parallelCountOption,
            nocacheOption,
        };

        buildCommand.SetHandler<string, string, string, ContentCompareType, string?, string, bool, int, int?>(BuildLocalizedIntelliSenseFile, packNameOption, monikerOption, localeOption, contentCompareTypeOption, separateLineOption, outputOption, nocacheOption, parallelCountOption, s_logLevelOption);

        return buildCommand;
    }

    private static void BuildLocalizedIntelliSenseFile(string packName,
                                                       string moniker,
                                                       string locale,
                                                       ContentCompareType contentCompareType,
                                                       string? separateLine,
                                                       string outputRoot,
                                                       bool noCache,
                                                       int parallelCount,
                                                       int? logLevel)
    {
        locale = string.IsNullOrWhiteSpace(locale) ? LocalizerEnvironment.CurrentLocale : locale;

        if (string.IsNullOrWhiteSpace(locale))
        {
            s_logger.LogCritical("\"locale\" must be specified.");
            Environment.Exit(1);
            return;
        }

        CultureInfo cultureInfo;
        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(locale);
        }
        catch
        {
            s_logger.LogCritical("\"{locale}\" is not a effective locale.", locale);
            Environment.Exit(1);
            throw;
        }

        if (!string.IsNullOrEmpty(packName)
            && !packName.EndsWith(".Ref", StringComparison.OrdinalIgnoreCase))
        {
            packName = $"{packName}.Ref";
        }

        outputRoot = string.IsNullOrWhiteSpace(outputRoot) ? LocalizerEnvironment.OutputRoot : outputRoot;

        DirectoryUtil.CheckDirectory(outputRoot);

        var packNameFilterFunc = BuildStringFilterFunc(packName);
        var monikerFilterFunc = BuildStringFilterFunc(moniker);

        var applicationPackDescriptors = DotNetEnvironmentUtil.GetAllApplicationPacks().ToArray();

        var refDescriptors = applicationPackDescriptors.Where(m => packNameFilterFunc(m.Name))
                                                       .SelectMany(m => m.Versions)
                                                       .SelectMany(m => m.Monikers)
                                                       .Where(m => monikerFilterFunc(m.Moniker))
                                                       .GroupBy(m => m.Moniker)
                                                       .SelectMany(GetDistinctApplicationPackRefMonikerDescriptors)
                                                       .SelectMany(m => m.Refs)
                                                       .Where(m => m.Culture is null)
                                                       .ToArray();

        if (!refDescriptors.Any())
        {
            s_logger.LogCritical("Not found localizeable files.");
            Environment.Exit(1);
        }

        s_logger.LogInformation("Start generate. PackName: {PackName}, Moniker: {Moniker}, Locale: {locale}, ContentCompareType: {ContentCompareType}.",
                                packName,
                                moniker,
                                locale,
                                contentCompareType);

        SetLogLevel(logLevel);

        GenerateAsync().Wait();

        async Task GenerateAsync()
        {
            var generator = s_serviceProvider.GetRequiredService<LocalizeIntelliSenseGenerator>();

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = parallelCount };

            //处理文件
            int refCount = 0;
            foreach (var refDescriptor in refDescriptors)
            {
                refCount++;
                var applicationPackRefMoniker = refDescriptor.OwnerMoniker;
                var moniker = applicationPackRefMoniker.Moniker;

                s_logger.LogInformation("Processing pack [{PackName}:{Moniker}]. Progress {packRefCount}/{packRefAll}.",
                                        refDescriptor.OwnerMoniker.OwnerVersion.OwnerPack.Name,
                                        applicationPackRefMoniker.Moniker,
                                        refCount,
                                        refDescriptors.Length);

                int intelliSenseFileCount = 0;
                await Parallel.ForEachAsync(refDescriptor.IntelliSenseFiles, parallelOptions, async (intelliSenseFileDescriptor, cancellationToken) =>
                {
                    var count = Interlocked.Increment(ref intelliSenseFileCount);

                    var applicationPackVersion = applicationPackRefMoniker.OwnerVersion;
                    var applicationPack = applicationPackVersion.OwnerPack;

                    var buildPath = Path.Combine(LocalizerEnvironment.BuildRoot, $"{moniker}@{locale}@{contentCompareType}", applicationPack.Name, intelliSenseFileDescriptor.FileName);

                    s_logger.LogInformation("Progress PackRef[{packRefCount}/{packRefAll}]->File[{fileCount}/{fileAll}]. Processing [{packName}:{version}:{name}] now.",
                                            refCount,
                                            refDescriptors.Length,
                                            count,
                                            refDescriptor.IntelliSenseFiles.Count,
                                            applicationPack.Name,
                                            refDescriptor.OwnerMoniker.OwnerVersion.Version,
                                            intelliSenseFileDescriptor.Name);

                    var context = new GenerateContext(intelliSenseFileDescriptor, contentCompareType, separateLine, buildPath, cultureInfo)
                    {
                        ParallelCount = parallelCount
                    };

                    await generator.GenerateAsync(context, default);
                });
            }

            //创建压缩文件
            var outputPackNames = refDescriptors.Select(m => $"{m.OwnerMoniker.Moniker}@{locale}@{contentCompareType}").ToHashSet();
            foreach (var outputPackName in outputPackNames)
            {
                s_logger.LogInformation("start create language pack for {outputPackName}.", outputPackName);

                var rootPath = Path.Combine(LocalizerEnvironment.BuildRoot, outputPackName);
                DirectoryUtil.CheckDirectory(rootPath);

                var finalZipFilePath = await PackLanguagePackAsync(rootPath, moniker, locale, contentCompareType, outputRoot);

                s_logger.LogWarning("localization pack is saved at {finalZipFilePath}.", finalZipFilePath);
            }
        }

        static IEnumerable<ApplicationPackRefMonikerDescriptor> GetDistinctApplicationPackRefMonikerDescriptors(IEnumerable<ApplicationPackRefMonikerDescriptor> descriptors)
        {
            var dic = new Dictionary<string, ApplicationPackRefMonikerDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in descriptors)
            {
                dic[descriptor.OwnerVersion.OwnerPack.Name] = descriptor;
            }
            return dic.Values;
        }
    }

    private static async Task<string> PackLanguagePackAsync(string sourceRootPath, string moniker, string locale, ContentCompareType contentCompareType, string outputRoot)
    {
        var cultureInfo = CultureInfo.GetCultureInfo(locale);

        var packs = Directory.GetDirectories(sourceRootPath).Select(m => Path.GetFileName(m)).ToList();

        var metadata = new Dictionary<string, string>()
        {
            { "CreateTime", DateTime.UtcNow.ToString("yyyy-mm-dd HH:mm:ss.fff")},
        };

        var languagePackManifest = new LanguagePackManifest(LanguagePackManifest.CurrentVersion, moniker, locale, contentCompareType, packs, metadata);

        await File.WriteAllTextAsync(Path.Combine(sourceRootPath, LanguagePackManifest.ManifestFileName), languagePackManifest.ToJson(), default);

        var packCsprojFullName = Path.Combine(sourceRootPath, PackCsprojFileName);

        await File.WriteAllTextAsync(packCsprojFullName, PackCsprojContent, default);

        var langPackVersion = new LangPackVersion(moniker, DateTime.UtcNow, contentCompareType, cultureInfo);
        var nugetVersion = langPackVersion.Encode();

        using var packProcess = Process.Start("dotnet", $"pack {packCsprojFullName} -o {outputRoot} -c Release --nologo /p:PackageId={NugetPackageName} /p:Version={nugetVersion}");

        await packProcess.WaitForExitAsync();

        if (packProcess.ExitCode != 0)
        {
            WriteMessageAndExit($"create package fail with code \"{packProcess.ExitCode}\"");
        }

        return Path.Combine(outputRoot, $"{NugetPackageName}.{nugetVersion}.nupkg");
    }

    #endregion Private 方法
}
