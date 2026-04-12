namespace SecurityRecap.Core.Interfaces;

public interface IClaudeApiService
{
    Task<string> AnalyzePdfAsync(byte[] pdfBytes, string systemPrompt);
    Task<string> ChatAsync(string systemPrompt, IEnumerable<ChatMessage> messages);
}
