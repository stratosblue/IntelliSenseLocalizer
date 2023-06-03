using System.Text.RegularExpressions;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public static class IntelliSenseNameUtil
{
    /// <summary>
    /// 获取用于文档查询的key
    /// </summary>
    /// <param name="intelliSenseItemDescriptor"></param>
    /// <returns></returns>
    public static string GetMicrosoftDocsQueryKey(this IntelliSenseItemDescriptor intelliSenseItemDescriptor)
    {
        var name = TrimParameters(intelliSenseItemDescriptor.OriginName);

        return intelliSenseItemDescriptor.MemberType switch
        {
            MemberType.Method => GetMethodQueryName(),
            MemberType.Field => GetFieldQueryName(),
            _ => GetQueryName(name),
        };

        static string GetQueryName(string name) => IntelliSenseNameUtil.NormalizeName(name);

        string GetMethodQueryName()
        {
            return NormalizeName(Regex.Replace(name, "`{2,100}\\d+$", string.Empty));
        }

        string GetFieldQueryName()
        {
            name = NormalizeName(name);
            return name[..name.LastIndexOf('.')];
        }
    }

    public static string GetNameInLink(string href)
    {
        var tindex = href.IndexOf('#');
        if (tindex > 0)
        {
            return href[(tindex + 1)..];
        }
        var findex = href.LastIndexOf('/');
        var sindex = href.IndexOf('?');
        if (findex > 0)
        {
            return sindex > 0 ? href[(findex + 1)..sindex] : href[(findex + 1)..];
        }
        return sindex > 0 ? href[..sindex] : href;
    }

    /// <summary>
    /// .Replace('`', '-')
    /// .Replace('#', '-')
    /// .Replace('{', '-')
    /// .Replace('}', '-')
    /// .Replace('<', '-')
    /// .Replace('>', '-')
    /// .Replace('@', '-')
    /// .Replace(',', '-')
    /// .ToLowerInvariant()
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string NormalizeName(string name)
    {
        return name.Replace('`', '-')
                   .Replace('#', '-')
                   .Replace('{', '-')
                   .Replace('}', '-')
                   .Replace('<', '-')
                   .Replace('>', '-')
                   .Replace('@', '-')
                   .Replace(',', '-')
                   .ToLowerInvariant();
    }

    public static string NormalizeNameInHtmlForKey(string name)
    {
        return NormalizeName(name).Replace('.', '-')
                                  .Replace('~', '-')
                                  .Replace('_', '-')
                                  .Replace("--", "-")
                                  .Replace("--", "-")
                                  .Replace("--", "-");
    }

    /// <summary>
    /// .Replace('`', '-')
    /// .Replace('.', '-')
    /// .Replace('。', '-')
    /// .Replace('@', '-')
    /// .Replace(',', '-')
    /// .Replace('#', '-')
    /// .Replace("{", "((")
    /// .Replace("}", "))")
    /// .Replace('[', '(')
    /// .Replace(']', ')')
    /// .Replace('_', '-')
    /// .Replace('~', '-')
    /// .Replace("--", "-")
    /// .Replace("--", "-")
    /// .Replace("--", "-")
    /// .ToLowerInvariant()
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string NormalizeOriginNameToUniqueKey(string name)
    {
        return name.Replace('`', '-')
                   .Replace('.', '-')
                   .Replace('。', '-')
                   .Replace('？', '?')
                   .Replace('@', '-')
                   .Replace(',', '-')
                   .Replace('#', '-')
                   .Replace("{", "((")
                   .Replace("}", "))")
                   .Replace('[', '(')
                   .Replace(']', ')')
                   .Replace('_', '-')
                   .Replace('~', '-')
                   .Replace("--", "-")
                   .Replace("--", "-")
                   .Replace("--", "-")
                   .ToLowerInvariant();
    }

    /// <summary>
    /// T:System.Enum -> System.Enum
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string TrimMemberPrefix(string name)
    {
        var colonIndex = name.IndexOf(':');
        if (colonIndex > 0)
        {
            return name[(colonIndex + 1)..];
        }
        return name;
    }

    /// <summary>
    /// M:System.EntryPointNotFoundException.#ctor(System.String) -> System.EntryPointNotFoundException.#ctor
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string TrimParameters(string name)
    {
        var colonIndex = name.IndexOf(':');
        var bracketsIndex = name.IndexOf('(');
        if (bracketsIndex != -1)
        {
            name = name[(colonIndex + 1)..bracketsIndex];
        }
        else
        {
            name = name[(colonIndex + 1)..];
        }

        return name;
    }
}
