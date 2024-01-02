using System.Globalization;
using System.Text.RegularExpressions;

namespace IntelliSenseLocalizer;

public class LangPackVersion
{
    public LangPackVersion(string moniker, DateTime time, ContentCompareType contentCompareType, CultureInfo culture)
    {
        Moniker = moniker;
        Time = time;
        ContentCompareType = contentCompareType;
        Culture = culture;
    }

    public string Moniker { get; }

    public DateTime Time { get; }

    public ContentCompareType ContentCompareType { get; }

    public CultureInfo Culture { get; }

    public string Encode()
    {
        var monikerVersion = Regex.Match(Moniker, "\\d+\\.+\\d").Value;

        return $"{monikerVersion}.0-{Culture.Name.ToLowerInvariant()}-{ContentCompareType}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public static LangPackVersion Decode(string packVersionString)
    {
        var span = packVersionString.AsSpan();

        //TODO 友好错误

        var index = span.IndexOf('-');
        var version = new Version(span.Slice(0, index).ToString());

        span = span.Slice(index + 1);

        index = span.LastIndexOf('-');
        var time = DateTime.ParseExact(span.Slice(index + 1, span.Length - index - 1), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        span = span.Slice(0, index);

        index = span.LastIndexOf('-');
        var contentCompareType = (ContentCompareType)Enum.Parse(typeof(ContentCompareType), span.Slice(index + 1, span.Length - index - 1));

        span = span.Slice(0, index);
        var culture = CultureInfo.GetCultureInfo(span.ToString());

        return new LangPackVersion($"net{version.Major}.{version.Minor}", time, contentCompareType, culture);
    }
}
