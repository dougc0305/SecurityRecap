namespace SecurityRecap.Core.Interfaces;

public interface IChatService
{
    Task<string> SendMessageAsync(Guid tenantId, Guid propertyId, string message, IEnumerable<ChatMessage>? conversationHistory);
}

public record ChatMessage(string Role, string Content);
