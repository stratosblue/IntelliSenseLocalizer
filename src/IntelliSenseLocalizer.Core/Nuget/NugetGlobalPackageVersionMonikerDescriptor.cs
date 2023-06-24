using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer.Nuget;

public class NugetGlobalPackageVersionMonikerDescriptor
{
    /// <summary>
    /// .net 名称
    /// </summary>
    public string Moniker { get; }

    public NugetGlobalPackageVersionDescriptor OwnerVersion { get; }

    /// <summary>
    /// 根目录
    /// </summary>
    public string RootPath { get; }

    public IReadOnlyList<IntelliSenseFileDescriptor> IntelliSenseFiles => _intelliSenseFiles ??= new List<IntelliSenseFileDescriptor>(EnumerateIntelliSenseFiles(this));

    private IReadOnlyList<IntelliSenseFileDescriptor>? _intelliSenseFiles;

    public NugetGlobalPackageVersionMonikerDescriptor(NugetGlobalPackageVersionDescriptor ownerVersion, string moniker, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(moniker))
        {
            throw new ArgumentException($"“{nameof(moniker)}”不能为 null 或空白。", nameof(moniker));
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException($"“{nameof(rootPath)}”不能为 null 或空白。", nameof(rootPath));
        }

        OwnerVersion = ownerVersion;
        RootPath = rootPath;
        Moniker = moniker;
    }

    public override string ToString()
    {
        return $"{Moniker}";
    }

    public static IEnumerable<IntelliSenseFileDescriptor> EnumerateIntelliSenseFiles(NugetGlobalPackageVersionMonikerDescriptor nugetGlobalPackageVersionMoniker)
    {
        var path = nugetGlobalPackageVersionMoniker.RootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var dllPath in Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var xmlPath = Path.ChangeExtension(dllPath, ".xml");
            if (File.Exists(xmlPath))
            {
                yield return new IntelliSenseFileDescriptor(Path.GetFileNameWithoutExtension(xmlPath), Path.GetFileName(xmlPath), xmlPath, nugetGlobalPackageVersionMoniker.OwnerVersion.OwnerPackage.NormalizedName, nugetGlobalPackageVersionMoniker.Moniker, null);
            }
        }
    }
}
