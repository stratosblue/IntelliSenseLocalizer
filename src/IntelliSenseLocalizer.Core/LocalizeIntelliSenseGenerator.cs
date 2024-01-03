using System.Globalization;
using System.Xml;

using IntelliSenseLocalizer.Models;

using Microsoft.Extensions.Logging;

namespace IntelliSenseLocalizer;

public class GenerateContext
{
    public ContentCompareType ContentCompareType { get; }

    public CultureInfo CultureInfo { get; }

    public IntelliSenseFileDescriptor Descriptor { get; }

    public string OutputPath { get; }

    public int ParallelCount { get; set; } = 2;

    public string? SeparateLine { get; }

    public GenerateContext(IntelliSenseFileDescriptor descriptor, ContentCompareType contentCompareType, string? separateLine, string outputPath, CultureInfo cultureInfo)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException($"“{nameof(outputPath)}”不能为 null 或空。", nameof(outputPath));
        }

        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ContentCompareType = contentCompareType;
        OutputPath = outputPath;
        SeparateLine = separateLine;
        CultureInfo = cultureInfo ?? throw new ArgumentNullException(nameof(cultureInfo));
    }
}

public class LocalizeIntelliSenseGenerator
{
    private readonly IIntelliSenseItemProvider _intelliSenseItemProvider;

    private readonly IIntelliSenseItemUpdaterFactory _intelliSenseItemUpdaterFactory;

    private readonly ILogger _logger;

    public LocalizeIntelliSenseGenerator(IIntelliSenseItemProvider intelliSenseItemProvider,
                                         IIntelliSenseItemUpdaterFactory intelliSenseItemUpdaterFactory,
                                         ILogger<LocalizeIntelliSenseGenerator> logger)
    {
        _intelliSenseItemProvider = intelliSenseItemProvider ?? throw new ArgumentNullException(nameof(intelliSenseItemProvider));
        _intelliSenseItemUpdaterFactory = intelliSenseItemUpdaterFactory ?? throw new ArgumentNullException(nameof(intelliSenseItemUpdaterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task GenerateAsync(GenerateContext context, CancellationToken cancellationToken)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(context.Descriptor.FilePath);

        using var intelliSenseItemUpdater = _intelliSenseItemUpdaterFactory.GetUpdater(context);

        var intelliSenseItems = _intelliSenseItemProvider.GetItems(xmlDocument, context.Descriptor).ToList();

        var parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = context.ParallelCount > 0 ? context.ParallelCount : 1,
            CancellationToken = cancellationToken,
        };

        var groups = intelliSenseItems.GroupBy(x => x.GetMicrosoftDocsQueryKey()).Reverse().ToArray();

        await Parallel.ForEachAsync(groups, parallelOptions, async (intelliSenseItemGroup, token) =>
        {
            try
            {
                await intelliSenseItemUpdater.UpdateAsync(intelliSenseItemGroup, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update IntelliSenseFile Group [{Key}]", intelliSenseItemGroup.Key);
            }
        });

        var outDir = Path.GetDirectoryName(context.OutputPath);
        DirectoryUtil.CheckDirectory(outDir);

        _logger.LogDebug("[{Name}] processing completed. Save the file into {OutputPath}.", context.Descriptor.Name, context.OutputPath);

        xmlDocument.Save(context.OutputPath);
    }
}
