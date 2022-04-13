using HtmlAgilityPack;

namespace IntelliSenseLocalizer.Models;

public class MSDocPageAnalysisResult
{
    public Dictionary<string, HtmlNode> Fields { get; set; }
    public Dictionary<string, HtmlNode> Parameters { get; }
    public HtmlNode? ReturnNode { get; set; }
    public HtmlNode? SummaryNode { get; set; }
    public string UniqueKey { get; }

    public MSDocPageAnalysisResult(string uniqueKey, Dictionary<string, HtmlNode> parameters, Dictionary<string, HtmlNode> fields)
    {
        UniqueKey = uniqueKey ?? throw new ArgumentNullException(nameof(uniqueKey));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }
}
