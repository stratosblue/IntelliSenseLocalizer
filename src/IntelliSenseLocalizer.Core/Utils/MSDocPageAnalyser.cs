using HtmlAgilityPack;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

/// <summary>
/// 微软文档页面分析器
/// </summary>
public static class MSDocPageAnalyser
{
    /// <summary>
    /// 分析页面文档并提供分析结果
    /// </summary>
    /// <param name="htmlDocument"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static MSDocPageAnalysisResult[] AnalysisHtmlDocument(HtmlDocument htmlDocument)
    {
        var htmlRootNode = htmlDocument.DocumentNode;

        if (htmlRootNode.SelectSingleNode("//html") is null)
        {
            throw new Exception($"没有HTML节点");
        }

        var memberNameHolderNodes = htmlRootNode.SelectNodes("//div[@class=\"memberNameHolder\"]");

        if (memberNameHolderNodes is null
            || memberNameHolderNodes.Count == 0)  //不包含多内容
        {
            var apiName = htmlDocument.DocumentNode.SelectSingleNode("//meta[@name=\"APIName\"]").GetAttributeValue("content", "");

            apiName = IntelliSenseNameUtil.NormalizeNameInHtmlForKey(apiName);

            var memberRootNode = htmlRootNode.SelectSingleNode("//div[@class=\"summaryHolder\"]")?.ParentNode
                                 ?? htmlRootNode.SelectSingleNode("//div[@class=\"content \"]")
                                 ?? htmlRootNode.SelectSingleNode("//div[@class=\"content\"]");

            return new[] { CreatePageAnalysisResult(apiName, memberRootNode) };
        }
        else
        {
            var result = new MSDocPageAnalysisResult[memberNameHolderNodes.Count];

            for (int i = 0; i < memberNameHolderNodes.Count; i++)
            {
                var memberNameHolderNode = memberNameHolderNodes[i];
                var uniqueKey = IntelliSenseNameUtil.NormalizeNameInHtmlForKey(memberNameHolderNode.SelectSingleNode("./h2").Id);

                if (memberNameHolderNode.GetNextTagNode("div") is HtmlNode memberInfoNode
                    && memberInfoNode.GetAttributeValue("class", string.Empty).EqualsOrdinalIgnoreCase("memberInfo"))
                {
                    result[i] = CreatePageAnalysisResult(uniqueKey, memberInfoNode);
                }
            }

            return result;
        }
    }

    private static MSDocPageAnalysisResult CreatePageAnalysisResult(string uniqueKey, HtmlNode htmlNode)
    {
        var summaryHolderNode = htmlNode.SelectSingleNode("./div[@class=\"summaryHolder\"]");

        var fields = GetFields(htmlNode);

        var parameterNodes = htmlNode.SelectNodes(".//dl[@class=\"parameterList\"]");

        Dictionary<string, HtmlNode> parameters = new();

        if (parameterNodes is not null)
        {
            foreach (var item in parameterNodes)
            {
                if (item.NextSibling.NextSibling is HtmlNode { Name: "p" } descNode)
                {
                    var parameterName = item.SelectSingleNode("./dt").InnerText.Trim();
                    parameters.Add(parameterName, descNode);
                }
            }
        }

        var returnPropertyInfoNode = htmlNode.SelectSingleNode("./dl[@class=\"propertyInfo\"]");
        var returnNode = returnPropertyInfoNode?.NextSibling?.NextSibling is HtmlNode tReturnNode
                         && tReturnNode.Name.EqualsOrdinalIgnoreCase("p")
                            ? tReturnNode
                            : null;

        return new MSDocPageAnalysisResult(uniqueKey, parameters, fields)
        {
            SummaryNode = summaryHolderNode,
            ReturnNode = returnNode,
        };
    }

    private static Dictionary<string, HtmlNode> GetFields(HtmlNode htmlNode)
    {
        var tableNodes = htmlNode.SelectNodes("./table[contains(@class,'nameValue')]");

        Dictionary<string, HtmlNode> fields = new();

        HtmlNode? fieldsTableNodes = null;

        if (tableNodes is not null)
        {
            foreach (var tableNode in tableNodes)
            {
                if (tableNode is not null
                    && tableNode.GetPreTagNode("h2") is HtmlNode h2Node
                    && (h2Node.Id.EqualsOrdinal("") || h2Node.Id.EqualsOrdinalIgnoreCase("fields")))
                {
                    var trs = tableNode.SelectNodes(".//tr");
                    foreach (var tr in trs)
                    {
                        var tds = tr.ChildNodes.Where(m => m.Name == "td").ToArray();
                        var key = tds.First().Id;

                        if (string.IsNullOrWhiteSpace(key))
                        {
                            var href = tds.First().SelectSingleNode(".//a")?.GetAttributeValue("href", "") ?? string.Empty;
                            key = IntelliSenseNameUtil.GetNameInLink(href);
                        }

                        if (string.IsNullOrWhiteSpace(key))
                        {
                            throw new Exception("字段key找不到");
                        }
                        key = IntelliSenseNameUtil.NormalizeNameInHtmlForKey(key);
                        fields.Add(key, tds.Last());
                    }
                }
            }
        }

        if (fieldsTableNodes is not null)
        {
            var trs = fieldsTableNodes.SelectNodes(".//tr");
            foreach (var tr in trs)
            {
                var tds = tr.ChildNodes.Where(m => m.Name == "td").ToArray();
                fields.Add(tds.First().Id, tds.Last());
            }
        }

        return fields;
    }
}