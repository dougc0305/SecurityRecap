namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Enums;

public interface IChatService
{
    Task<string> SendMessageAsync(Guid tenantId, Guid userId, UserRole userRole, Guid propertyId, string message, IEnumerable<ChatMessage>? conversationHistory);
}

public record ChatMessage(string Role, string Content);
