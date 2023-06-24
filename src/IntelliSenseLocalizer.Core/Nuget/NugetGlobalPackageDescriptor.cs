using IntelliSenseLocalizer.Utils;

namespace IntelliSenseLocalizer.Nuget;

public class NugetGlobalPackageDescriptor
{
    public string NormalizedName { get; set; }

    public string RootPath { get; set; }

    /// <summary>
    /// 应用程序包版本描述列表
    /// </summary>
    public IReadOnlyList<NugetGlobalPackageVersionDescriptor> Versions => _versions ??= new List<NugetGlobalPackageVersionDescriptor>(EnumerateNugetGlobalPackageVersions(this));

    private IReadOnlyList<NugetGlobalPackageVersionDescriptor>? _versions;

    public NugetGlobalPackageDescriptor(string normalizedName, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException($"“{nameof(normalizedName)}”不能为 null 或空白。", nameof(normalizedName));
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException($"“{nameof(rootPath)}”不能为 null 或空白。", nameof(rootPath));
        }

        NormalizedName = normalizedName;
        RootPath = rootPath;
    }

    public override string ToString()
    {
        return $"{NormalizedName}";
    }

    public static IEnumerable<NugetGlobalPackageVersionDescriptor> EnumerateNugetGlobalPackageVersions(NugetGlobalPackageDescriptor nugetGlobalPackage)
    {
        var path = nugetGlobalPackage.RootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        //loop for path like %userprofile%\.nuget\packages\*\*
        foreach (var versionPath in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (VersionUtil.TryParse(Path.GetFileName(versionPath), out var version))
            {
                yield return new NugetGlobalPackageVersionDescriptor(nugetGlobalPackage, version, versionPath);
            }
        }
    }
}
