using System.Xml;

using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public interface IIntelliSenseItemProvider
{
    IEnumerable<IntelliSenseItemDescriptor> GetItems(XmlDocument xmlDocument, IntelliSenseFileDescriptor intelliSenseFileDescriptor);
}
