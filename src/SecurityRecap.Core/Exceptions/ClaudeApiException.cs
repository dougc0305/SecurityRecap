namespace SecurityRecap.Core.Exceptions;

using System.Net;

public enum ClaudeFailureKind
{
    Unknown,
    Billing,
    RateLimit,
    AuthenticationOrPermission,
    BadRequest,
    Overloaded,
    Network,
    Timeout
}

public class ClaudeApiException : Exception
{
    public ClaudeFailureKind Kind { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? AnthropicErrorType { get; }
    public string? AnthropicMessage { get; }
    public string? RequestId { get; }

    public ClaudeApiException(
        ClaudeFailureKind kind,
        string? anthropicErrorType,
        string? anthropicMessage,
        string? requestId,
        HttpStatusCode? statusCode,
        Exception? inner = null)
        : base(BuildMessage(kind, anthropicErrorType, anthropicMessage, requestId, statusCode), inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        AnthropicErrorType = anthropicErrorType;
        AnthropicMessage = anthropicMessage;
        RequestId = requestId;
    }

    public string Code => Kind switch
    {
        ClaudeFailureKind.Billing => "CLAUDE_BILLING",
        ClaudeFailureKind.RateLimit => "CLAUDE_RATE_LIMIT",
        ClaudeFailureKind.AuthenticationOrPermission => "CLAUDE_AUTH",
        ClaudeFailureKind.BadRequest => "CLAUDE_BAD_REQUEST",
        ClaudeFailureKind.Overloaded => "CLAUDE_OVERLOADED",
        ClaudeFailureKind.Network => "CLAUDE_NETWORK",
        ClaudeFailureKind.Timeout => "CLAUDE_TIMEOUT",
        _ => "CLAUDE_UNKNOWN"
    };

    private static string BuildMessage(
        ClaudeFailureKind kind, string? type, string? message, string? requestId, HttpStatusCode? status)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Claude API failure (").Append(kind).Append(')');
        if (status.HasValue) sb.Append(" status=").Append((int)status.Value);
        if (!string.IsNullOrEmpty(type)) sb.Append(" type=").Append(type);
        if (!string.IsNullOrEmpty(requestId)) sb.Append(" request_id=").Append(requestId);
        if (!string.IsNullOrEmpty(message)) sb.Append(" — ").Append(message);
        return sb.ToString();
    }
}
