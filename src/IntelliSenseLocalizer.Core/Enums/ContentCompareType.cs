namespace IntelliSenseLocalizer;

/// <summary>
/// 内容对照类型
/// </summary>
public enum ContentCompareType
{
    /// <summary>
    /// 默认
    /// </summary>
    Default = OriginFirst,

    /// <summary>
    /// 原始内容靠前
    /// </summary>
    OriginFirst = 1,

    /// <summary>
    /// 本地化内容靠前
    /// </summary>
    LocaleFirst = 2,

    /// <summary>
    /// 无原始内容+
    /// 对照
    /// </summary>
    None = 3,
}
