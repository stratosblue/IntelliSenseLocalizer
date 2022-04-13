using System.Globalization;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemUpdaterFactory
{
    IIntelliSenseItemUpdater GetUpdater(CultureInfo cultureInfo);
}