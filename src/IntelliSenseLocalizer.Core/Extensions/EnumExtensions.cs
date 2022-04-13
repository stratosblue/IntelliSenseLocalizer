using System.Reflection;

using IntelliSenseLocalizer;

namespace IntelliSenseLocalizer;

public static class EnumExtensions
{
    public static T? GetCustomAttribute<T>(this Enum enumValue) where T : Attribute
    {
        var type = enumValue.GetType();
        var enumName = Enum.GetName(type, enumValue);  //获取对应的枚举名
        if (enumName is null)
        {
            return default;
        }
        var fieldInfo = type.GetField(enumName);
        if (fieldInfo is null)
        {
            return default;
        }
        var attribute = fieldInfo.GetCustomAttribute(typeof(T), false);
        if (attribute is null)
        {
            return default;
        }
        return (T)attribute;
    }
}