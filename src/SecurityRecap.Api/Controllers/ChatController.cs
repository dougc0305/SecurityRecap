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

        try
        {
            var response = await _chatService.SendMessageAsync(
                tenantId, request.PropertyId, request.Message,
                request.ConversationHistory?.Select(h => new ChatMessage(h.Role, h.Content)));
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
