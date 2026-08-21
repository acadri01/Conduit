namespace Conduit.Core.NeutralFiles;

/// <summary>Thrown when a <c>.cii</c> file doesn't match the expected neutral file structure.</summary>
public sealed class NeutralFileParseException : Exception
{
    public NeutralFileParseException(string message) : base(message)
    {
    }

    public NeutralFileParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
