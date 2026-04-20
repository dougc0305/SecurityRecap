using SecurityRecap.Core.Exceptions;

namespace SecurityRecap.Api.Services;

public static class ClaudeFailureFormatter
{
    public static string ToUserMessage(ClaudeApiException ex)
    {
        var lead = ex.Kind switch
        {
            ClaudeFailureKind.Billing =>
                "The AI service is temporarily unavailable because the account credit balance has run out.",
            ClaudeFailureKind.RateLimit =>
                "The AI service is busy. Please wait a moment and try again.",
            ClaudeFailureKind.AuthenticationOrPermission =>
                "The AI service rejected our credentials. The administrator needs to update the API key.",
            ClaudeFailureKind.Overloaded =>
                "The AI service is overloaded right now. Please try again in a minute.",
            ClaudeFailureKind.Timeout =>
                "The AI service didn't respond in time. Please try again.",
            ClaudeFailureKind.Network =>
                "Couldn't reach the AI service. Please try again.",
            ClaudeFailureKind.BadRequest =>
                "The AI service rejected the request. Please contact support.",
            _ =>
                "The AI service returned an unexpected error. Please contact support."
        };

        var detail = string.IsNullOrEmpty(ex.RequestId)
            ? $"Reference: {ex.Code}"
            : $"Reference: {ex.Code} / {ex.RequestId}";

        return $"{lead} Please share this with support: {detail}.";
    }
}
