namespace IntelliSenseLocalizer.Models;

/// <summary>
/// 描述 C:\Program Files\dotnet\packs\*.App.Ref\* 例如: C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4
/// </summary>
public class ApplicationPackVersionDescriptor : IEquatable<ApplicationPackVersionDescriptor>
{
    private List<ApplicationPackRefMonikerDescriptor>? _monikers;

    /// <summary>
    /// .Net名称列表
    /// </summary>
    public IReadOnlyList<ApplicationPackRefMonikerDescriptor> Monikers => _monikers ??= new List<ApplicationPackRefMonikerDescriptor>(EnumerateApplicationPackRefMonikers(this));

    /// <summary>
    /// 所属包
    /// </summary>
    public ApplicationPackDescriptor OwnerPack { get; }

    /// <summary>
    /// ref根目录
    /// </summary>
    public string RefRootPath { get; }

    /// <summary>
    /// 此包的根目录
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// .net 版本
    /// </summary>
    public Version Version { get; }

    public ApplicationPackVersionDescriptor(ApplicationPackDescriptor ownerPack, Version version, string rootPath)
    {
        OwnerPack = ownerPack;
        Version = version;
        RootPath = rootPath;
        RefRootPath = Path.Combine(RootPath, "ref");
    }

    public static IEnumerable<ApplicationPackRefMonikerDescriptor> EnumerateApplicationPackRefMonikers(ApplicationPackVersionDescriptor applicationPackVersion)
    {
        var path = applicationPackVersion.RefRootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        //loop for file like C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\*
        foreach (var monikerPath in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            var moniker = Path.GetFileName(monikerPath);
            yield return new ApplicationPackRefMonikerDescriptor(applicationPackVersion, moniker, monikerPath);
        }
    }

    public override string ToString()
    {
        return $"[{Version}] at [{RootPath}]";
    }

    #region Equals

    public bool Equals(ApplicationPackVersionDescriptor? other)
    {
        return other is not null
               && Version.Equals(other.Version)
               && string.Equals(RootPath, other.RootPath);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ApplicationPackVersionDescriptor);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Version, RootPath);
    }

    #endregion Equals
}
