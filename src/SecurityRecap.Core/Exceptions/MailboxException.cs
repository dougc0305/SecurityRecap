namespace SecurityRecap.Core.Exceptions;

public enum MailboxFailureKind
{
    Unknown,
    Authentication,
    Permission,
    MailboxNotFound,
    FolderNotFound,
    Network,
    Timeout,
    Throttled,
    Configuration
}

public class MailboxException : Exception
{
    public MailboxException(
        MailboxFailureKind kind,
        string message,
        string? graphErrorCode = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        GraphErrorCode = graphErrorCode;
    }

    public MailboxFailureKind Kind { get; }
    public string? GraphErrorCode { get; }
}
