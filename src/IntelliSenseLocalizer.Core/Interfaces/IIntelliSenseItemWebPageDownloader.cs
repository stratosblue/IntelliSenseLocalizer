using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemWebPageDownloader : IDisposable
{
    Task<string> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default);
}