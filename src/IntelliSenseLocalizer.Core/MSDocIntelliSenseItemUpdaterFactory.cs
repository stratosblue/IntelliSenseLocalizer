using System.Globalization;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

/// <summary>
/// 基于微软文档的更新器工厂
/// </summary>
public class MSDocIntelliSenseItemUpdaterFactory : IIntelliSenseItemUpdaterFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public MSDocIntelliSenseItemUpdaterFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IIntelliSenseItemUpdater GetUpdater(CultureInfo cultureInfo)
    {
        return new MSDocIntelliSenseItemUpdater(cultureInfo, _loggerFactory.CreateLogger<MSDocIntelliSenseItemUpdater>());
    }
}