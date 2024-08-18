using AlibabaCloud.SDK.Alimt20190107.Models;

namespace SimpleTranslateServer;

//TODO 手写以支持AOT

public class AlibabaCloudTranslator : ITranslator
{
    #region Private 字段

    private readonly AlibabaCloud.SDK.Alimt20190107.Client _client;

    #endregion Private 字段

    #region Public 构造函数

    public AlibabaCloudTranslator(string accessKeyId, string accessKeySecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeySecret);

        var aliClientConfig = new AlibabaCloud.OpenApiClient.Models.Config
        {
            AccessKeyId = accessKeyId,
            AccessKeySecret = accessKeySecret,
            Endpoint = "mt.aliyuncs.com"
        };

        _client = new AlibabaCloud.SDK.Alimt20190107.Client(aliClientConfig);
    }

    #endregion Public 构造函数

    #region Public 方法

    public async Task<string> TranslateAsync(string content, string from, string to, CancellationToken cancellationToken)
    {
        content = $"<p>{content}</p>";

        var request = new TranslateGeneralRequest
        {
            SourceLanguage = from.Split('-').First().ToLowerInvariant(),
            TargetLanguage = to.Split('-').First().ToLowerInvariant(),
            SourceText = content,
            //FormatType = "text",
            FormatType = "html",
            Scene = "general"
        };

        var response = await _client.TranslateGeneralAsync(request);
        var result = response.Body.Data.Translated;

        if (result.StartsWith("<p>"))
        {
            result = result[3..];
        }
        if (result.EndsWith("</p>"))
        {
            result = result[..^4];
        }
        return result;
    }

    #endregion Public 方法
}
