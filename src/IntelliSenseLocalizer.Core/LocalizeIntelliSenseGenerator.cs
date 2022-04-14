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

    public GenerateContext(IntelliSenseFileDescriptor descriptor, ContentCompareType contentCompareType, string outputPath, CultureInfo cultureInfo)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException($"“{nameof(outputPath)}”不能为 null 或空。", nameof(outputPath));
        }

        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ContentCompareType = contentCompareType;
        OutputPath = outputPath;
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

        var intelliSenseItemUpdater = _intelliSenseItemUpdaterFactory.GetUpdater(context);

        var intelliSenseItems = _intelliSenseItemProvider.GetItems(xmlDocument, context.Descriptor).ToList();

        var parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = context.ParallelCount > 0 ? context.ParallelCount : 1,
            CancellationToken = cancellationToken,
        };

        var groups = intelliSenseItems.GroupBy(x => x.GetMicrosoftDocsQueryKey()).ToArray();

        await Parallel.ForEachAsync(groups, parallelOptions, async (intelliSenseItemGroup, token) =>
        {
            try
            {
                await intelliSenseItemUpdater.UpdateAsync(intelliSenseItemGroup, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Update IntelliSenseFile Group [{Key}] Fail: {Message}", intelliSenseItemGroup.Key, ex.Message);
            }
        });

        var outDir = Path.GetDirectoryName(context.OutputPath);
        if (outDir is not null
            && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }
        xmlDocument.Save(context.OutputPath);
    }
}