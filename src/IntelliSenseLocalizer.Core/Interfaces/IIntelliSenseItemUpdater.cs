using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemUpdater
{
    /// <summary>
    /// 更新<paramref name="intelliSenseItemGroup"/>中的所有项
    /// </summary>
    /// <param name="intelliSenseItemGroup"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateAsync(IGrouping<string, IntelliSenseItemDescriptor> intelliSenseItemGroup, CancellationToken cancellationToken);
}