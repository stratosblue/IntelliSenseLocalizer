namespace IntelliSenseLocalizer.Nuget;

public class NugetGlobalPackageVersionDescriptor
{
    public NugetGlobalPackageVersionDescriptor(NugetGlobalPackageDescriptor ownerPackage, Version version, string rootPath)
    {
        OwnerPackage = ownerPackage;
        Version = version;
        RootPath = rootPath;
    }

    public NugetGlobalPackageDescriptor OwnerPackage { get; }

    public Version Version { get; }

    public string RootPath { get; }

    public override string ToString()
    {
        return $"{Version}";
    }

    public IReadOnlyList<NugetGlobalPackageVersionMonikerDescriptor> Monikers => _versions ??= new List<NugetGlobalPackageVersionMonikerDescriptor>(EnumerateNugetGlobalPackageVersionMonikers(this));

    private IReadOnlyList<NugetGlobalPackageVersionMonikerDescriptor>? _versions;

    public static IEnumerable<NugetGlobalPackageVersionMonikerDescriptor> EnumerateNugetGlobalPackageVersionMonikers(NugetGlobalPackageVersionDescriptor nugetGlobalPackageVersion)
    {
        var path = Path.Combine(nugetGlobalPackageVersion.RootPath, "lib");
        if (!Directory.Exists(path))
        {
            yield break;
        }

        //loop for path like %userprofile%\.nuget\packages\xxx\1.0.0\lib\*
        foreach (var monikerPath in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            yield return new NugetGlobalPackageVersionMonikerDescriptor(nugetGlobalPackageVersion, Path.GetFileName(monikerPath), monikerPath);
        }
    }
}
