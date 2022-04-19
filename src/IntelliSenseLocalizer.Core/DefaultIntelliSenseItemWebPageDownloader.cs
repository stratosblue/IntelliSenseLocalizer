using System.Globalization;

using Cuture.Http;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public sealed class DefaultIntelliSenseItemWebPageDownloader : IIntelliSenseItemWebPageDownloader
{
    public const string NotFoundPageContent = "404NotFound";
    private readonly string _cacheRoot;
    private readonly string _locale;
    private readonly SemaphoreSlim _parallelSemaphore;

    public DefaultIntelliSenseItemWebPageDownloader(CultureInfo cultureInfo, string cacheRoot, int parallelCount)
    {
        _locale = cultureInfo.Name.ToLowerInvariant();

        _cacheRoot = cacheRoot;
        _parallelSemaphore = new SemaphoreSlim(parallelCount, parallelCount);

        DirectoryUtil.CheckDirectory(cacheRoot);
    }

    public void Dispose()
    {
        _parallelSemaphore.Dispose();
    }

    public async Task<string> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default)
    {
        var queryKey = memberDescriptor.GetMicrosoftDocsQueryKey();
        var intelliSenseFile = memberDescriptor.IntelliSenseFileDescriptor;
        var frameworkMoniker = intelliSenseFile.OwnerPackRef.OwnerMoniker.Moniker;
        var cacheDriectory = Path.Combine(_cacheRoot, intelliSenseFile.OwnerPackRef.OwnerMoniker.OwnerVersion.OwnerPack.Name, frameworkMoniker, _locale);
        var cacheFilePath = Path.Combine(cacheDriectory, $"{queryKey}.html");

        var url = $"https://docs.microsoft.com/{_locale}/dotnet/api/{queryKey}?view={frameworkMoniker}";

        if (!ignoreCache
            && File.Exists(cacheFilePath))
        {
            var existedHtml = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            if (existedHtml.EqualsOrdinalIgnoreCase(NotFoundPageContent))
            {
                throw NotFoundException();
            }
            return existedHtml;
        }

        HttpOperationResult<string> response;
        await _parallelSemaphore.WaitAsync(cancellationToken);
        try
        {
            response = await url.CreateHttpRequest()
                                .UseUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.52")
                                .UseSystemProxy()
                                .AutoRedirection()
                                .WithCancellation(cancellationToken)
                                .TryGetAsStringAsync();
        }
        finally
        {
            _parallelSemaphore.Release();
        }

        using var disposable = response;

        if (response.Exception is not null)
        {
            throw response.Exception;
        }

        if (response.ResponseMessage?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await File.WriteAllTextAsync(cacheFilePath, NotFoundPageContent, cancellationToken);
            throw NotFoundException();
        }

        response.ResponseMessage!.EnsureSuccessStatusCode();

        var html = response.Data!;
        DirectoryUtil.CheckDirectory(cacheDriectory);

        await File.WriteAllTextAsync(cacheFilePath, html, cancellationToken);

        return html;

        MSOnlineDocNotFoundException NotFoundException()
        {
            return new MSOnlineDocNotFoundException($"{url}");
        }
    }
}