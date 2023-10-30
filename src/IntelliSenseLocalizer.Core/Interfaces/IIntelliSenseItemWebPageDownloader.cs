using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemWebPageDownloader : IDisposable
{
    Task<(string html, string url)> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default);
}
