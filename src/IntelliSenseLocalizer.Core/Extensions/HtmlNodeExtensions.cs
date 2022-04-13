namespace HtmlAgilityPack;

internal static class HtmlNodeExtensions
{
    public static HtmlNode? GetNextTagNode(this HtmlNode htmlNode, string tagName)
    {
        while (htmlNode.NextSibling is HtmlNode nextNode)
        {
            if (string.Equals(nextNode.Name, tagName, StringComparison.OrdinalIgnoreCase))
            {
                return nextNode;
            }

            htmlNode = nextNode;
        }

        return null;
    }

    public static HtmlNode? GetPreTagNode(this HtmlNode htmlNode, string tagName)
    {
        while (htmlNode.PreviousSibling is HtmlNode preNode)
        {
            if (string.Equals(preNode.Name, tagName, StringComparison.OrdinalIgnoreCase))
            {
                return preNode;
            }

            htmlNode = preNode;
        }

        return null;
    }
}