namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemUpdaterFactory
{
    IIntelliSenseItemUpdater GetUpdater(GenerateContext generateContext);
}
