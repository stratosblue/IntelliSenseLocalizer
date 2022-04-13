using System.Xml;

namespace IntelliSenseLocalizer.Models;

public record class IntelliSenseItemDescriptor(IntelliSenseFileDescriptor IntelliSenseFileDescriptor, string OriginName, string UniqueKey, MemberType MemberType, XmlElement Element)
{
    public static IntelliSenseItemDescriptor Create(XmlElement Element, IntelliSenseFileDescriptor intelliSenseFileDescriptor)
    {
        var name = Element.GetAttribute("name");

        if (string.IsNullOrEmpty(name))
        {
            throw new Exception();
        }

        MemberType memberType = name[0] switch
        {
            'M' => MemberType.Method,
            'T' => MemberType.Type,
            'P' => MemberType.Property,
            'E' => MemberType.Event,
            'F' => MemberType.Field,
            _ => throw new Exception(name)
        };

        name = name.Substring(2);

        var uniqueKey = IntelliSenseNameUtil.NormalizeOriginNameToUniqueKey(name);

        return new(intelliSenseFileDescriptor, name, uniqueKey, memberType, Element);
    }
}