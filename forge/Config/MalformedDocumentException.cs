using System;

namespace Config;

/// <summary>
/// Raised by <see cref="ProjectConfigurationParser"/> when a file is not well-formed, or uses a
/// construct with no JSON equivalent (S6.11). Carries a 1-based line/column so the caller can name
/// the position in the <see cref="ConfigErrorCode.MalformedDocument"/> message.
/// </summary>
internal sealed class MalformedDocumentException : Exception
{
    public long Line { get; }
    public long Column { get; }

    public MalformedDocumentException(string message, long line, long column)
        : base(message)
    {
        Line = line;
        Column = column;
    }
}
