namespace IntelliSenseLocalizer;

[Serializable]
public class IntelliSenseLocalizerException : Exception
{
    protected IntelliSenseLocalizerException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

    public IntelliSenseLocalizerException()
    { }

    public IntelliSenseLocalizerException(string message) : base(message)
    {
    }

    public IntelliSenseLocalizerException(string message, Exception inner) : base(message, inner)
    {
    }
}
