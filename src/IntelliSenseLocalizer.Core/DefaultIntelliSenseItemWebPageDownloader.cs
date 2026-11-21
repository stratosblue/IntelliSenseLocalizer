using System.Globalization;

using Cuture.Http;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public sealed class DefaultIntelliSenseItemWebPageDownloader : IIntelliSenseItemWebPageDownloader
{
    #region Public 字段

    public const string NotFoundPageContent = "404NotFound";

    #endregion Public 字段

    #region Private 字段

    private readonly string _cacheRoot;

    private readonly string _locale;

    private readonly SemaphoreSlim _parallelSemaphore;

    #endregion Private 字段

    #region Public 构造函数

    public DefaultIntelliSenseItemWebPageDownloader(CultureInfo cultureInfo, string cacheRoot, int parallelCount)
    {
        _locale = cultureInfo.Name.ToLowerInvariant();

        _cacheRoot = cacheRoot;
        _parallelSemaphore = new SemaphoreSlim(parallelCount, parallelCount);

        DirectoryUtil.CheckDirectory(cacheRoot);
    }

    #endregion Public 构造函数

    #region Public 方法

    public void Dispose()
    {
        _parallelSemaphore.Dispose();
    }

    public async Task<(string html, string url)> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default)
    {
        var queryKey = memberDescriptor.GetMicrosoftDocsQueryKey();
        var intelliSenseFile = memberDescriptor.IntelliSenseFileDescriptor;
        var frameworkMoniker = intelliSenseFile.Moniker;
        var cacheDriectory = Path.Combine(_cacheRoot, intelliSenseFile.Name, frameworkMoniker, _locale);
        var cacheFilePath = Path.Combine(cacheDriectory, $"{queryKey}.html");

        DirectoryUtil.CheckDirectory(cacheDriectory);

        static string MonikerView(IntelliSenseItemDescriptor memberDescriptor, string frameworkMoniker)
        {
            var packName = memberDescriptor.IntelliSenseFileDescriptor.PackName;
            if (packName.StartsWith("Microsoft.AspNetCore"))
            {
                return frameworkMoniker.Replace("net", "aspnetcore-");
            }
            else if (packName.StartsWith("Microsoft.WindowsDesktop"))
            {
                return frameworkMoniker.Replace("net", "windowsdesktop-");
            }
            else
            {
                return frameworkMoniker.Replace("net", "net-");
            }
        }

        var url = $"https://learn.microsoft.com/{_locale}/dotnet/api/{queryKey}?view={MonikerView(memberDescriptor, frameworkMoniker)}";

        if (!ignoreCache
            && File.Exists(cacheFilePath))
        {
            var existedHtml = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            if (existedHtml.EqualsOrdinalIgnoreCase(NotFoundPageContent))
            {
                throw NotFoundException();
            }
            return (existedHtml, url);
        }

        HttpOperationResult<string> response;

        using var localCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localCTS.CancelAfter(TimeSpan.FromSeconds(60));

        using var request = url.CreateHttpRequest(true)
                               .UseUserAgent(UserAgents.Chrome)
                               .UseSystemProxy()
                               .AutoRedirection()
                               .WithCancellation(localCTS.Token);

        await _parallelSemaphore.WaitAsync(cancellationToken);
        try
        {
            response = await request.TryGetAsStringAsync();
            if (!response.IsSuccessStatusCode
                && response.ResponseMessage?.StatusCode != System.Net.HttpStatusCode.NotFound)  //非成功状态再尝试一次
            {
                response = await request.TryGetAsStringAsync();
            }
        }
        finally
        {
            _parallelSemaphore.Release();
        }

        using var disposable = response;

        if (response.ResponseMessage?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await File.WriteAllTextAsync(cacheFilePath, NotFoundPageContent, cancellationToken);
            throw NotFoundException();
        }

        if (response.Exception is not null)
        {
            throw response.Exception;
        }

        response.ResponseMessage!.EnsureSuccessStatusCode();

        var html = response.Data!;

        await File.WriteAllTextAsync(cacheFilePath, html, cancellationToken);

        return (html, url);

        MSOnlineDocNotFoundException NotFoundException()
        {
            return new MSOnlineDocNotFoundException($"{url}");
        }
    }

    #endregion Public 方法
}
