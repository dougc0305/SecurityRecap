using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Infrastructure.Services;

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<ClaudeApiService> _logger;

    public ClaudeApiService(HttpClient httpClient, IConfiguration config, ILogger<ClaudeApiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("Anthropic API key not configured. Set Anthropic:ApiKey or ANTHROPIC_API_KEY env var.");
        _logger = logger;
    }

    public async Task<string> AnalyzePdfAsync(byte[] pdfBytes, string systemPrompt)
    {
        var pdfBase64 = Convert.ToBase64String(pdfBytes);

        var requestBody = new
        {
            model = "claude-opus-4-5",
            max_tokens = 8192,
            system = systemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "document",
                            source = new
                            {
                                type = "base64",
                                media_type = "application/pdf",
                                data = pdfBase64
                            }
                        },
                        new
                        {
                            type = "text",
                            source = (object?)null,
                            text = "Analyze this security patrol report PDF and extract all structured data as specified."
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        _logger.LogInformation("Sending PDF ({Size} bytes) to Claude API for analysis", pdfBytes.Length);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Claude API returned {response.StatusCode}: {responseBody}");
        }

        return ExtractTextContent(responseBody);
    }

    public async Task<string> ChatAsync(string systemPrompt, IEnumerable<ChatMessage> messages)
    {
        var messageList = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToArray();

        var requestBody = new
        {
            model = "claude-sonnet-4-5",
            max_tokens = 4096,
            system = systemPrompt,
            messages = messageList
        };

        var json = JsonSerializer.Serialize(requestBody);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        _logger.LogInformation("Sending chat message to Claude API");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Claude API returned {response.StatusCode}: {responseBody}");
        }

        return ExtractTextContent(responseBody);
    }

    private static string ExtractTextContent(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var contentArray = doc.RootElement.GetProperty("content");

        foreach (var block in contentArray.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                return block.GetProperty("text").GetString()
                    ?? throw new InvalidOperationException("Claude returned empty text");
            }
        }

        throw new InvalidOperationException("No text content in Claude response");
    }
}
