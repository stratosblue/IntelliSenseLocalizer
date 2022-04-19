namespace HtmlAgilityPack;

internal static class HtmlNodeExtensions
{
    public static HtmlNode? GetNextTagNode(this HtmlNode htmlNode, string tagName)
    {
        while (htmlNode.NextSibling is HtmlNode nextNode)
        {
            if (nextNode.Name.EqualsOrdinalIgnoreCase(tagName))
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
            if (preNode.Name.EqualsOrdinalIgnoreCase(tagName))
            {
                return preNode;
            }

            htmlNode = preNode;
        }

        return null;
    }
}