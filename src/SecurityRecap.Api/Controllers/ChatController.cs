using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize]
public class ChatController : BaseApiController
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> SendMessage([FromBody] ChatRequest request)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();

        if (request.ConversationHistory is not null)
        {
            foreach (var item in request.ConversationHistory)
            {
                if (!string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<ChatResponse>.Fail("Conversation history contains an invalid role."));
                }

                if (string.IsNullOrWhiteSpace(item.Content))
                    return BadRequest(ApiResponse<ChatResponse>.Fail("Conversation history contains an empty message."));
            }
        }

        try
        {
            var response = await _chatService.SendMessageAsync(
                tenantId,
                userId,
                userRole,
                request.PropertyId,
                request.Message,
                request.ConversationHistory?.Select(h => new ChatMessage(h.Role.ToLowerInvariant(), h.Content)));
            return Ok(ApiResponse<ChatResponse>.Ok(new ChatResponse(response)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ChatResponse>.Fail(ex.Message));
        }
    }
}

public record ChatRequest(
    [Required] Guid PropertyId,
    [Required, MaxLength(4000)] string Message,
    List<ChatHistoryItem>? ConversationHistory
);

public record ChatHistoryItem(string Role, string Content);

public record ChatResponse(string Response);
