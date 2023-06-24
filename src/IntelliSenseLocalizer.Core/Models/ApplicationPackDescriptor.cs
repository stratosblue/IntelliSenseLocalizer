using IntelliSenseLocalizer.Utils;

namespace IntelliSenseLocalizer.Models;

/// <summary>
/// 描述 C:\Program Files\dotnet\packs\*.App.Ref 例如 C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref
/// </summary>
public class ApplicationPackDescriptor : IEquatable<ApplicationPackDescriptor>
{
    private IReadOnlyList<ApplicationPackVersionDescriptor>? _versions;

    /// <summary>
    /// 应用程序包名称 eg: Microsoft.NETCore.App
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 此包的根目录
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// 应用程序包版本描述列表
    /// </summary>
    public IReadOnlyList<ApplicationPackVersionDescriptor> Versions => _versions ??= new List<ApplicationPackVersionDescriptor>(EnumerateApplicationPackVersions(this));

    public ApplicationPackDescriptor(string name, string rootPath)
    {
        Name = name;
        RootPath = rootPath;
    }

    public static IEnumerable<ApplicationPackVersionDescriptor> EnumerateApplicationPackVersions(ApplicationPackDescriptor applicationPack)
    {
        var path = applicationPack.RootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        //loop for file like C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\*
        foreach (var versionPath in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (VersionUtil.TryParse(Path.GetFileName(versionPath), out var version))
            {
                yield return new ApplicationPackVersionDescriptor(applicationPack, version, versionPath);
            }
        }
    }

    public override string ToString()
    {
        return $"[{Name}] at [{RootPath}]";
    }

    #region Equals

    public bool Equals(ApplicationPackDescriptor? other)
    {
        return other is not null
               && string.Equals(Name, other.Name)
               && string.Equals(RootPath, other.RootPath);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ApplicationPackDescriptor);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, RootPath);
    }

    #endregion Equals
}
