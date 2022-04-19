using System.Net;
using System.Xml;

using HtmlAgilityPack;

using IntelliSenseLocalizer.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntelliSenseLocalizer;

/// <summary>
/// 基于微软文档的更新器
/// </summary>
public class MSDocIntelliSenseItemUpdater : IIntelliSenseItemUpdater
{
    private readonly ContentCompareType _contentCompareType;
    private readonly IIntelliSenseItemWebPageDownloader _downloader;
    private readonly GenerateContext _generateContext;
    private readonly ILogger _logger;
    private readonly string? _separateLine;
    private bool _disposedValue;

    public MSDocIntelliSenseItemUpdater(GenerateContext generateContext, ILogger logger)
    {
        _generateContext = generateContext ?? throw new ArgumentNullException(nameof(generateContext));
        _contentCompareType = generateContext.ContentCompareType;
        _separateLine = generateContext.SeparateLine;

        _logger = logger ?? NullLogger.Instance;
        _downloader = new DefaultIntelliSenseItemWebPageDownloader(generateContext.CultureInfo, LocalizerEnvironment.CacheRoot, generateContext.ParallelCount);
    }

    public async Task UpdateAsync(IGrouping<string, IntelliSenseItemDescriptor> intelliSenseItemGroup, CancellationToken cancellationToken)
    {
        var currentGroupItems = intelliSenseItemGroup.Reverse().ToArray();

        _logger.LogTrace("Start Process IntelliSense Item Group [{Key}] GroupSize: {Size}", intelliSenseItemGroup.Key, currentGroupItems.Length);

        try
        {
            var html = await _downloader.DownloadAsync(currentGroupItems.First(), false, default);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var analysisResults = MSDocPageAnalyser.AnalysisHtmlDocument(htmlDocument);

            //原始数据和分析结果都只有一个，直接处理
            if (analysisResults.Length == 1 && currentGroupItems.Length == 1)
            {
                UpdateIntelliSenseItem(currentGroupItems[0], analysisResults[0]);
                return;
            }

            //匹配多个结果进行处理
            var analysisResultDictionary = analysisResults.ToDictionary(m => m.UniqueKey);
            foreach (var member in currentGroupItems)
            {
                if (!analysisResultDictionary.TryGetValue(member.UniqueKey, out var pageAnalysisResult))    //处理字段
                {
                    var fieldsPageAnalysisResult = analysisResults.Where(m => m.Fields.ContainsKey(member.UniqueKey)).FirstOrDefault();
                    if (fieldsPageAnalysisResult is null)
                    {
                        _logger.LogDebug("Can not found analysis result for {Key}.", member.UniqueKey);
                        continue;
                    }

                    UpdateIntelliSenseItem(member, fieldsPageAnalysisResult);
                }
                else    //处理方法等成员
                {
                    analysisResultDictionary.Remove(member.UniqueKey);
                    UpdateIntelliSenseItem(member, pageAnalysisResult);
                }
            }
        }
        catch (MSOnlineDocNotFoundException)
        {
            var descriptor = currentGroupItems.First();
            _logger.LogDebug("Can not found online doc for {MemberType}:{OriginName} - {Key}", descriptor.MemberType, descriptor.OriginName, intelliSenseItemGroup.Key);
        }
    }

    #region Process

    /// <summary>
    /// 使用<paramref name="refDictionary"/>中<paramref name="refKey"/>项的值，替换<paramref name="linkHtmlNode"/>中的对应内容
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="refDictionary"></param>
    /// <param name="linkHtmlNode"></param>
    /// <param name="refKey"></param>
    protected virtual void ReplaceRefNodeContent(IntelliSenseItemDescriptor descriptor, Dictionary<string, XmlNode> refDictionary, HtmlNode linkHtmlNode, string refKey)
    {
        if (!refDictionary.TryGetValue(refKey, out var node))
        {
            _logger.LogDebug("Not found ref key {RefKey} for {Name}.", refKey, descriptor.OriginName);
            return;
        }
        linkHtmlNode.RemoveAll();
        linkHtmlNode.AppendChild(HtmlNode.CreateNode(WebUtility.HtmlEncode(node.OuterXml)));
    }

    /// <summary>
    /// 使用<paramref name="htmlNode"/>更新<paramref name="element"/>的信息
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="element"></param>
    /// <param name="htmlNode"></param>
    protected virtual void UpdateElementContent(IntelliSenseItemDescriptor descriptor, XmlElement element, HtmlNode htmlNode)
    {
        var originNodes = element.ChildNodes.ToList();

        if (_contentCompareType == ContentCompareType.OriginFirst)
        {
            AppendSeparateLine();
        }

        XmlNode? appendLastNode = null;

        if (element.ChildNodes.Count > 1)
        {
            //原始数据最后为一个节点为换行节点
            if (element.ChildNodes[element.ChildNodes.Count - 1] is XmlElement lastElement
                && lastElement.Name.EqualsOrdinalIgnoreCase("para"))
            {
                appendLastNode = lastElement.CloneNode(true);
            }

            var itemClone = (XmlElement)element.CloneNode(true);
            var refDictionary = itemClone.CreateRefDictionary();

            if (refDictionary.Count > 0)
            {
                if (htmlNode.SelectNodes(".//a[@data-linktype!='external']") is HtmlNodeCollection aNodes)
                {
                    foreach (var linkHtmlNode in aNodes)
                    {
                        var linkKey = IntelliSenseNameUtil.NormalizeOriginNameToUniqueKey(IntelliSenseNameUtil.GetNameInLink(linkHtmlNode.GetAttributeValue("href", "")));
                        ReplaceRefNodeContent(descriptor, refDictionary, linkHtmlNode, linkKey);
                    }
                }
                else if (htmlNode.SelectNodes(".//code") is HtmlNodeCollection codeNodes)
                {
                    foreach (var linkHtmlNode in codeNodes)
                    {
                        var linkKey = IntelliSenseNameUtil.NormalizeOriginNameToUniqueKey(linkHtmlNode.InnerText);
                        ReplaceRefNodeContent(descriptor, refDictionary, linkHtmlNode, linkKey);
                    }
                }
            }
        }

        var contentLines = WebUtility.HtmlDecode(htmlNode.InnerText.Trim()).Split('\n');

        for (int contentIndex = 0; contentIndex < contentLines.Length; contentIndex++)
        {
            var contentLine = contentLines[contentIndex];

            if (contentIndex > 0)
            {
                element.AppendChild(element.CreateParaNode());
            }

            if (contentLine.Contains('<'))
            {
                var tmpDoc = new XmlDocument();

                tmpDoc.LoadXml($"<xml>{contentLine}</xml>");

                foreach (XmlNode tmpDocItem in tmpDoc.ChildNodes[0]!.ChildNodes)
                {
                    element.ImportAppendChild(tmpDocItem);
                }
            }
            else
            {
                element.AppendChild(element.OwnerDocument.CreateTextNode(contentLine));
            }
        }

        if (appendLastNode is not null)
        {
            element.AppendChild(appendLastNode);
        }

        //原内容在前，不需要更多处理
        if (_contentCompareType != ContentCompareType.OriginFirst)
        {
            //移除原内容
            foreach (var item in originNodes)
            {
                element.RemoveChild(item);
            }
            //本地化内容在前，把原内容添加到最后
            if (_contentCompareType == ContentCompareType.LocaleFirst)
            {
                AppendSeparateLine();

                foreach (var item in originNodes)
                {
                    element.AppendChild(item);
                }
            }
        }

        //添加换行分割
        void AppendSeparateLine()
        {
            element.AppendChild(element.CreateParaNode());
            if (string.IsNullOrWhiteSpace(_separateLine))
            {
                return;
            }

            element.AppendChild(element.OwnerDocument.CreateTextNode(_separateLine));

            element.AppendChild(element.CreateParaNode());
        }
    }

    /// <summary>
    /// 更新<paramref name="descriptor"/>的信息
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="analysisResult"></param>
    protected virtual void UpdateIntelliSenseItem(IntelliSenseItemDescriptor descriptor, MSDocPageAnalysisResult analysisResult)
    {
        switch (descriptor.MemberType)
        {
            case MemberType.Field:
                {
                    RunAndLogExceptionsAsDebug(() => UpdateElementsContent(descriptor.Element.GetSummaryNodes(), analysisResult.Fields[descriptor.UniqueKey]), $"Process {descriptor.OriginName}'s Field Summary Node");
                }
                break;

            case MemberType.Method:
            case MemberType.Type:
            case MemberType.Property:
            case MemberType.Event:
            default:
                {
                    if (analysisResult.SummaryNode is not null)
                    {
                        RunAndLogExceptionsAsDebug(() => UpdateElementsContent(descriptor.Element.GetSummaryNodes(), analysisResult.SummaryNode), $"{descriptor.OriginName}'s Summary Node");
                    }
                    else
                    {
                        LogNotFoundNodeInPageAnalysisResult(descriptor, nameof(analysisResult.SummaryNode));
                    }

                    if (descriptor.Element.GetReturnsNodes() is XmlNodeList returnsNodeList
                        && returnsNodeList.Count > 0)
                    {
                        if (analysisResult.ReturnNode is not null)
                        {
                            RunAndLogExceptionsAsDebug(() => UpdateElementsContent(returnsNodeList, analysisResult.ReturnNode), $"{descriptor.OriginName}'s Return Node");
                        }
                        else
                        {
                            LogNotFoundNodeInPageAnalysisResult(descriptor, nameof(analysisResult.ReturnNode));
                        }
                    }

                    if (analysisResult.Parameters.Count > 0)
                    {
                        RunAndLogExceptionsAsDebug(() => UpdateParameterNodes(descriptor, descriptor.Element.GetParamNodes(), analysisResult), $"{descriptor.OriginName}'s ParamNode Node");

                        RunAndLogExceptionsAsDebug(() => UpdateParameterNodes(descriptor, descriptor.Element.GetTypeParamNodes(), analysisResult), $"{descriptor.OriginName}'s TypeParamNode Node");
                    }
                }
                break;
        }

        void UpdateElementsContent(XmlNodeList elements, HtmlNode htmlNode)
        {
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var item = (XmlElement)elements[i]!;
                UpdateElementContent(descriptor, item, htmlNode);
            }
        }
    }

    /// <summary>
    /// 更新<paramref name="paramNodes"/>的参数列表
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="paramNodes"></param>
    /// <param name="analysisResult"></param>
    ///
    protected virtual void UpdateParameterNodes(IntelliSenseItemDescriptor descriptor, XmlNodeList paramNodes, MSDocPageAnalysisResult analysisResult)
    {
        for (int i = 0; i < paramNodes.Count; i++)
        {
            var item = (XmlElement)paramNodes[i]!;

            var name = item.GetAttribute("name");
            if (analysisResult.Parameters.TryGetValue(name, out var htmlNode))
            {
                UpdateElementContent(descriptor, item, htmlNode);
            }
        }
    }

    #endregion Process

    #region Base

    private void LogNotFoundNodeInPageAnalysisResult(IntelliSenseItemDescriptor member, string nodeName)
    {
        _logger.LogDebug("Not found {NodeName} for member {OriginName} in page analysis result.", nodeName, member.OriginName);
    }

    private void RunAndLogExceptionsAsDebug(Action action, string description)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Exception at process [{Description}] {Message}", description, ex.Message);
        }
    }

    #endregion Base

    #region dispose

    ~MSDocIntelliSenseItemUpdater()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
            _downloader.Dispose();
        }
    }

    #endregion dispose
}