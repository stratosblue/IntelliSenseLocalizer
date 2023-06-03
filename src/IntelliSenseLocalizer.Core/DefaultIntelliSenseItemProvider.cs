using System.Xml;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public class DefaultIntelliSenseItemProvider : IIntelliSenseItemProvider
{
    public IEnumerable<IntelliSenseItemDescriptor> GetItems(XmlDocument xmlDocument, IntelliSenseFileDescriptor intelliSenseFileDescriptor)
    {
        var memberNodeList = xmlDocument.GetElementsByTagName("member");

        foreach (XmlElement member in memberNodeList)
        {
            yield return IntelliSenseItemDescriptor.Create(member, intelliSenseFileDescriptor);
        }
    }
}
