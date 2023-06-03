using System.Reflection;

namespace IntelliSenseLocalizer;

/// <summary>
/// 标记.net名称
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
public sealed class FrameworkMonikerAttribute : Attribute
{
    public string Moniker { get; }

    public FrameworkMonikerAttribute(string moniker)
    {
        if (string.IsNullOrWhiteSpace(moniker))
        {
            throw new ArgumentException($"“{nameof(moniker)}”不能为 null 或空白。", nameof(moniker));
        }

        Moniker = moniker;
    }
}

public static class FrameworkMonikerAttributeExtensions
{
    public static string? GetFrameworkMoniker(this Type type)
    {
        if (type.GetCustomAttribute<FrameworkMonikerAttribute>() is FrameworkMonikerAttribute frameworkMonikerAttribute)
        {
            return frameworkMonikerAttribute.Moniker;
        }
        return null;
    }

    public static string? GetFrameworkMoniker(this Enum enumValue)
    {
        if (enumValue.GetCustomAttribute<FrameworkMonikerAttribute>() is FrameworkMonikerAttribute frameworkMonikerAttribute)
        {
            return frameworkMonikerAttribute.Moniker;
        }
        return null;
    }
}
