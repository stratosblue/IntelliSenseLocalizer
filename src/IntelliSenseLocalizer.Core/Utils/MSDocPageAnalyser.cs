using System.Xml;

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
    public static MSDocPageAnalysisResult[] AnalysisHtmlDocument(string url, HtmlDocument htmlDocument, IntelliSenseItemDescriptor[] intelliSenseItems)
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

            var version = intelliSenseItems.First().IntelliSenseFileDescriptor.Moniker[^3..];

            var memberRootNodes = htmlRootNode.SelectNodes("//div[@class=\"memberInfo\"]")?
                                    .Where(x => x.ParentNode.Attributes["data-moniker"].Value.Split(" ").Any(z => z.EndsWith(version)));
            if( memberRootNodes?.Any() == true )
            {
                return memberRootNodes.Select( x => {
                        var id = x.ParentNode.SelectSingleNode(".//h2[@class=\"memberNameHolder\"]").Id;
                        var uniqueKey = IntelliSenseNameUtil.NormalizeNameInHtmlForKey(id);

                        return CreatePageAnalysisResult(url, uniqueKey, x, intelliSenseItems);
                    } ).ToArray();
            }

            var memberRootNode = htmlRootNode.SelectSingleNode("//div[@class=\"content \"]")?.SelectSingleNode(".//div[@class=\"summaryHolder\"]")?.ParentNode
                                ?? htmlRootNode.SelectSingleNode("//div[@class=\"content\"]")?.SelectSingleNode(".//div[@class=\"summaryHolder\"]")?.ParentNode
                                 ?? htmlRootNode.SelectSingleNode("//div[@class=\"content \"]")
                                 ?? htmlRootNode.SelectSingleNode("//div[@class=\"content\"]");

            return new[] { CreatePageAnalysisResult(url, apiName, memberRootNode, intelliSenseItems) };
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
                    result[i] = CreatePageAnalysisResult(url, uniqueKey, memberInfoNode, intelliSenseItems);
                }
            }

            return result;
        }
    }

    private static MSDocPageAnalysisResult CreatePageAnalysisResult(string url, string uniqueKey, HtmlNode htmlNode, IntelliSenseItemDescriptor[] intelliSenseItems)
    {
        var summaryHolderNode = htmlNode.SelectSingleNode("./div[@class=\"summaryHolder\"]");

        var fields = GetFields(htmlNode);

        var parameterNodes = htmlNode.SelectNodes(".//dl[@class=\"parameterList\"]");

        Dictionary<string, HtmlNode> parameters = new();

        if (parameterNodes is not null)
        {
            foreach (var item in parameterNodes)
            {
                var parameterName = item.SelectNodes("./dt/span")?.FirstOrDefault(x => {
                                        var version = intelliSenseItems.First().IntelliSenseFileDescriptor.Moniker[^3..];
                                        return x.Attributes["data-moniker"].Value.Split(" ").Any(z => z.EndsWith(version));
                                    } )?.InnerText.Trim();

                parameterName ??= item.SelectSingleNode("./dt").InnerText.Trim();

                var descNodes = item.SelectNodes(".//following-sibling::p");
                if(descNodes?.Any() == true)
                {
                    if(descNodes.Count == 1)
                    {
                        parameters.TryAdd(parameterName, descNodes[0]);
                    }
                    else
                    {
                        var newNode = HtmlNode.CreateNode("<span></span>");
                            newNode.AppendChildren( descNodes );

                        parameters.TryAdd(parameterName, newNode);
                    }
                }
                else
                {
                    parameters.TryAdd(parameterName, HtmlNode.CreateNode("<p tags=\"emptyNode\" />"));
                }
            }
        }

        var currentGroupItem = intelliSenseItems.FirstOrDefault(x => {
                                    return x.Element.GetParamNodes().Cast<XmlElement>()
                                                    .Concat(x.Element.GetTypeParamNodes().Cast<XmlElement>())
                                                    .Select(z => z.Attributes["name"]?.Value)
                                                    .OrderBy(z => z)
                                                    .SequenceEqual(parameters.Keys.OrderBy(z => z));
                                } ) 
                                ?? intelliSenseItems.FirstOrDefault( x => {
                                    return x.Element.GetParamNodes().Cast<XmlElement>()
                                                    .Concat(x.Element.GetTypeParamNodes().Cast<XmlElement>())
                                                    .Select(z => z.Attributes["name"]?.Value)
                                                    .OrderBy(z => z)
                                                    .SequenceEqual(parameters.Where(z => z.Value.Attributes["tags"]?.Value != "emptyNode").Select(z => z.Key).OrderBy(z => z));
                                } );

        var returnNode = (HtmlNode?)null;

        if(currentGroupItem?.Element.GetReturnsNodes().Count > 0) {
            returnNode = htmlNode.SelectSingleNode("./dl[@class=\"propertyInfo\"]/following-sibling::p")

                        ?? htmlNode.SelectNodes(".//h4")
                                .ElementAtOrDefault(parameters.Any() ? 1 : 0)?
                                .SelectSingleNode(".//following-sibling::p")

                        ?? HtmlNode.CreateNode("<p/>");
        }

        return new MSDocPageAnalysisResult(url, uniqueKey, parameters, fields)
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
                        fields.TryAdd(key, tds.Last());
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
