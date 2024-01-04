namespace GnuCashSharp;

public class GncException : Exception
{
    public GncException(string message)
        : base(message)
    {
    }

    public GncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
