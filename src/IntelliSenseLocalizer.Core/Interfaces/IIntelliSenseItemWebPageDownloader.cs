using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemWebPageDownloader
{
    Task<string> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default);
}