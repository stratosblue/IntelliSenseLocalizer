using System.Globalization;

namespace IntelliSenseLocalizer.Models;

/// <summary>
/// 描述 C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\* 例如: C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\net6.0
/// </summary>
public class ApplicationPackRefDescriptor : IEquatable<ApplicationPackRefDescriptor>
{
    private HashSet<IntelliSenseFileDescriptor>? _intelliSenseFiles;

    /// <summary>
    /// 对应的区域
    /// </summary>
    public CultureInfo? Culture { get; }

    /// <summary>
    /// 文件列表
    /// </summary>
    public HashSet<IntelliSenseFileDescriptor> IntelliSenseFiles => _intelliSenseFiles ??= new HashSet<IntelliSenseFileDescriptor>(EnumerateIntelliSenseFiles(this));

    public ApplicationPackRefMonikerDescriptor OwnerMoniker { get; }

    /// <summary>
    /// 根目录
    /// </summary>
    public string RootPath { get; }

    public ApplicationPackRefDescriptor(ApplicationPackRefMonikerDescriptor ownerMoniker, CultureInfo? culture, string rootPath)
    {
        OwnerMoniker = ownerMoniker;
        Culture = culture;
        RootPath = rootPath;
    }

    public static IEnumerable<IntelliSenseFileDescriptor> EnumerateIntelliSenseFiles(ApplicationPackRefDescriptor applicationPackRef)
    {
        var path = applicationPackRef.RootPath;
        if (!Directory.Exists(path))
        {
            yield break;
        }

        //loop for file like C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.4\ref\net6.0\zh-cn\*.xml
        foreach (var intelliSenseFilePath in Directory.EnumerateFiles(path, "*.xml", SearchOption.TopDirectoryOnly))
        {
            var intelliSenseName = Path.GetFileNameWithoutExtension(intelliSenseFilePath);
            var intelliSenseFileName = Path.GetFileName(intelliSenseFilePath);
            yield return new IntelliSenseFileDescriptor(applicationPackRef, intelliSenseName, intelliSenseFileName, intelliSenseFilePath);
        }
    }

    public override string ToString()
    {
        return $"[{Culture}] at [{RootPath}]";
    }

    #region Equals

    public bool Equals(ApplicationPackRefDescriptor? other)
    {
        return other is not null
               && string.Equals(RootPath, other.RootPath)
               && (Culture is null ? other.Culture is null : Culture.Equals(other.Culture));
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ApplicationPackRefDescriptor);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RootPath, Culture?.GetHashCode() ?? 0);
    }

    #endregion Equals
}