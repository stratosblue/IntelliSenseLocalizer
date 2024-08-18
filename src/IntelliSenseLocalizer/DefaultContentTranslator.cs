using System.Globalization;
using System.Text;
using System.Web;
using IntelliSenseLocalizer.ThirdParty;

namespace IntelliSenseLocalizer;

internal sealed class DefaultContentTranslator : IContentTranslator, IDisposable
{
    #region Private 字段

    private readonly HttpClient _httpClient;

    #endregion Private 字段

    #region Public 构造函数

    public DefaultContentTranslator(string address)
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri(address)
        };
    }

    #endregion Public 构造函数

    #region Public 方法

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<string> TranslateAsync(string content, CultureInfo from, CultureInfo to, CancellationToken cancellationToken = default)
    {
        using var responseMessage = await _httpClient.PostAsync($"/?from={HttpUtility.UrlEncode(from.Name)}&to={HttpUtility.UrlEncode(to.Name)}", new ByteArrayContent(Encoding.UTF8.GetBytes(content)), cancellationToken);
        return await responseMessage.Content.ReadAsStringAsync(cancellationToken);
    }

    #endregion Public 方法
}
