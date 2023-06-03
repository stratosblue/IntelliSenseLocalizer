using System.Globalization;

namespace IntelliSenseLocalizer.Models;

/// <summary>
/// 描述 C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\* 例如: C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\net6.0
/// </summary>
public class ApplicationPackRefMonikerDescriptor : IEquatable<ApplicationPackRefMonikerDescriptor>
{
    private List<ApplicationPackRefDescriptor>? _refs;

    /// <summary>
    /// .net 名称
    /// </summary>
    public string Moniker { get; }

    public ApplicationPackVersionDescriptor OwnerVersion { get; }

    /// <summary>
    /// 应用程序包引用列表
    /// </summary>
    public IReadOnlyList<ApplicationPackRefDescriptor> Refs => _refs ??= new List<ApplicationPackRefDescriptor>(EnumerateApplicationPackRefs(this));

    /// <summary>
    /// 根目录
    /// </summary>
    public string RootPath { get; }

    public ApplicationPackRefMonikerDescriptor(ApplicationPackVersionDescriptor ownerVersion, string moniker, string rootPath)
    {
        OwnerVersion = ownerVersion;
        RootPath = rootPath;
        Moniker = moniker;
    }

    public static IEnumerable<ApplicationPackRefDescriptor> EnumerateApplicationPackRefs(ApplicationPackRefMonikerDescriptor packRefMoniker)
    {
        var path = packRefMoniker.RootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        yield return new ApplicationPackRefDescriptor(packRefMoniker, null, path);

        //loop for file like C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\net6.0\*
        foreach (var refDirectory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            var cultureName = Path.GetFileName(refDirectory);
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(cultureName);
            }
            catch
            {
                continue;
            }
            yield return new ApplicationPackRefDescriptor(packRefMoniker, culture, refDirectory);
        }
    }

    public override string ToString()
    {
        return $"[{Moniker}] at [{RootPath}]";
    }

    #region Equals

    public bool Equals(ApplicationPackRefMonikerDescriptor? other)
    {
        return other is not null
               && string.Equals(Moniker, other.Moniker)
               && string.Equals(RootPath, other.RootPath);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ApplicationPackRefMonikerDescriptor);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Moniker, RootPath);
    }

    #endregion Equals
}
