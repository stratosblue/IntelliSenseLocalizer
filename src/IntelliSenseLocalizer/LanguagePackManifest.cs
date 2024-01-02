using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelliSenseLocalizer;

/// <summary>
/// 语言包清单信息
/// </summary>
/// <param name="version"></param>
/// <param name="moniker"></param>
/// <param name="cultureName"></param>
/// <param name="contentCompareType"></param>
/// <param name="packs"></param>
/// <param name="metadata"></param>
public class LanguagePackManifest(uint version, string moniker, string cultureName, ContentCompareType contentCompareType, IReadOnlyList<string> packs, IReadOnlyDictionary<string, string> metadata)
{
    public const uint CurrentVersion = 1;

    public const string ManifestFileName = "islocalizer.manifest.json";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        IncludeFields = true,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    [JsonIgnore]
    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);

    public string CultureName { get; } = cultureName;

    public IReadOnlyDictionary<string, string> Metadata { get; } = metadata.ToImmutableDictionary();

    public IReadOnlyList<string> Packs { get; } = packs.ToImmutableArray();

    public uint Version { get; } = version;

    public string Moniker { get; } = moniker;

    public ContentCompareType ContentCompareType { get; } = contentCompareType;

    public static LanguagePackManifest FromJson(string json)
    {
        var jsonDocument = JsonDocument.Parse(json);
        if (!jsonDocument.RootElement.TryGetProperty("Version", out var versionNode)
            || !versionNode.TryGetUInt32(out var version))
        {
            throw new ArgumentException($"not found field \"Version\" in json \"{json}\"");
        }

        return version switch
        {
            CurrentVersion => jsonDocument.Deserialize<LanguagePackManifest>()!,
            _ => throw new InvalidOperationException($"unsupported version \"{version}\""),
        };
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, GetType(), s_jsonSerializerOptions);
    }
}
