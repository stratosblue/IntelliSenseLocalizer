using System.Runtime.Serialization;

namespace IntelliSenseLocalizer;

[Serializable]
public class MSOnlineDocNotFoundException : IntelliSenseLocalizerException
{
    protected MSOnlineDocNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public MSOnlineDocNotFoundException()
    {
    }

    public MSOnlineDocNotFoundException(string message) : base(message)
    {
    }

    public MSOnlineDocNotFoundException(string message, Exception inner) : base(message, inner)
    {
    }
}
