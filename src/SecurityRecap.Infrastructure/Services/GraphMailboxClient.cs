using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Infrastructure.Services;

/// <summary>
/// Microsoft Graph mailbox access using an app-only (client credentials) token.
/// Raw HTTP rather than the Graph SDK, to stay consistent with how this codebase
/// calls the Claude API and to keep the dependency surface small.
/// </summary>
public class GraphMailboxClient : IMailboxClient
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    // Tokens are per app registration, valid ~1h, and shared across properties using the same app.
    private static readonly ConcurrentDictionary<string, CachedToken> TokenCache = new();

    private readonly HttpClient _http;
    private readonly ILogger<GraphMailboxClient> _logger;

    public GraphMailboxClient(HttpClient http, ILogger<GraphMailboxClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    private sealed record CachedToken(string AccessToken, DateTime ExpiresAtUtc);

    // ---------------------------------------------------------------- auth

    private async Task<string> GetTokenAsync(MailboxCredentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentials.TenantId)
            || string.IsNullOrWhiteSpace(credentials.ClientId)
            || string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new MailboxException(MailboxFailureKind.Configuration,
                "Graph tenant id, client id and client secret must all be configured.");
        }

        var cacheKey = credentials.TenantId + "|" + credentials.ClientId;
        if (TokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            return cached.AccessToken;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(credentials.TenantId)}/oauth2/v2.0/token";

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _http.PostAsync(url, form, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new MailboxException(MailboxFailureKind.Timeout, "Timed out requesting a Microsoft Graph token.", null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new MailboxException(MailboxFailureKind.Network, $"Could not reach Microsoft Entra: {ex.Message}", null, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var (code, description) = ReadTokenError(body);
            _logger.LogError("Graph token request failed {StatusCode} {Code}: {Description}",
                response.StatusCode, code, description);
            throw new MailboxException(MailboxFailureKind.Authentication,
                "Microsoft Entra rejected the app credentials: " + (description ?? response.StatusCode.ToString()), code);
        }

        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new MailboxException(MailboxFailureKind.Authentication, "Entra returned an empty access token.");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        TokenCache[cacheKey] = new CachedToken(token, DateTime.UtcNow.AddSeconds(expiresIn));
        return token;
    }

    private static (string? Code, string? Description) ReadTokenError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var code = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            var description = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;

            // Entra descriptions are multi-line with correlation ids; the first line is the useful part.
            if (description is not null)
            {
                var newline = description.IndexOfAny(new[] { '\r', '\n' });
                if (newline > 0) description = description.Substring(0, newline);
            }

            return (code, description);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    // ---------------------------------------------------------------- requests

    private async Task<HttpResponseMessage> SendAsync(
        MailboxCredentials credentials, HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetTokenAsync(credentials, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new MailboxException(MailboxFailureKind.Timeout, "Microsoft Graph request timed out.", null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new MailboxException(MailboxFailureKind.Network, $"Could not reach Microsoft Graph: {ex.Message}", null, ex);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string context, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        string? code = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("code", out var c)) code = c.GetString();
                if (err.TryGetProperty("message", out var m)) message = m.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body; fall through with what we have.
        }

        var kind = Classify(response.StatusCode, code);
        _logger.LogError("Graph {Context} failed {StatusCode} kind={Kind} code={Code}: {Message}",
            context, response.StatusCode, kind, code, message);

        throw new MailboxException(kind, BuildMessage(kind, context, code, message, response.StatusCode), code);
    }

    private static MailboxFailureKind Classify(HttpStatusCode status, string? code) => (status, code) switch
    {
        (HttpStatusCode.Unauthorized, _) => MailboxFailureKind.Authentication,
        (HttpStatusCode.Forbidden, _) => MailboxFailureKind.Permission,
        (HttpStatusCode.NotFound, "ErrorItemNotFound") => MailboxFailureKind.FolderNotFound,
        (HttpStatusCode.NotFound, _) => MailboxFailureKind.MailboxNotFound,
        (HttpStatusCode.TooManyRequests, _) => MailboxFailureKind.Throttled,
        (HttpStatusCode.RequestTimeout, _) => MailboxFailureKind.Timeout,
        (HttpStatusCode.GatewayTimeout, _) => MailboxFailureKind.Timeout,
        _ => MailboxFailureKind.Unknown
    };

    private static string BuildMessage(
        MailboxFailureKind kind, string context, string? code, string? message, HttpStatusCode status)
    {
        var detail = message ?? code ?? status.ToString();
        return kind switch
        {
            MailboxFailureKind.Permission =>
                "Microsoft Graph denied access to the mailbox. Confirm the app registration has the "
                + "Mail.Read (and Mail.Send, if summary email is enabled) application permission with admin consent, "
                + "and that any application access policy includes this mailbox. Graph said: " + detail,
            MailboxFailureKind.MailboxNotFound =>
                "Microsoft Graph could not find that mailbox. Check the address resolves to a mailbox in this "
                + "tenant; a shared mailbox is fine and does not need a license. Graph said: " + detail,
            MailboxFailureKind.FolderNotFound =>
                "Microsoft Graph could not find that mail folder. Graph said: " + detail,
            MailboxFailureKind.Throttled =>
                "Microsoft Graph is throttling requests; the next poll will retry. Graph said: " + detail,
            _ => $"Microsoft Graph {context} failed: {detail}"
        };
    }

    // ---------------------------------------------------------------- API surface

    public async Task<string> VerifyAccessAsync(
        MailboxCredentials credentials, string folderName, CancellationToken ct = default)
    {
        // Reads the folder rather than the user object on purpose: this needs only Mail.Read,
        // the same permission polling uses, so a passing test means polling will work. Looking
        // the user up instead would additionally require User.Read.All.
        var folder = string.IsNullOrWhiteSpace(folderName) ? "inbox" : folderName;
        var url = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/mailFolders/{Encode(folder)}"
            + "?$select=displayName,totalItemCount";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(credentials, request, ct);
        await EnsureSuccessAsync(response, "folder lookup", ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var display = doc.RootElement.TryGetProperty("displayName", out var d) ? d.GetString() : folder;
        var total = doc.RootElement.TryGetProperty("totalItemCount", out var t) && t.TryGetInt32(out var n)
            ? (int?)n
            : null;

        return total is null
            ? $"{credentials.MailboxAddress} / {display}"
            : $"{credentials.MailboxAddress} / {display} ({total} item(s))";
    }

    public async Task<IReadOnlyList<MailboxMessage>> FetchMessagesAsync(
        MailboxCredentials credentials, MailboxQuery query, CancellationToken ct = default)
    {
        // Exchange rejects a restriction on hasAttachments or from combined with an $orderby on
        // receivedDateTime — "The restriction or sort order is too complex for this operation".
        // So receivedDateTime is the only server-side filter, matching the sort key, and the
        // sender/attachment/subject predicates are applied client-side below. The mailbox is
        // dedicated to these reports, so the extra rows fetched are few and carry no bodies.
        var filter = "receivedDateTime ge " + query.ReceivedAfterUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Ascending on purpose. The caller advances its watermark across the contiguous run of
        // messages it handled, so if this page is truncated by $top the unfetched remainder is
        // strictly newer and gets collected on the next poll. Descending would hand back the
        // newest messages and let the watermark jump past older ones that were never seen.
        var folder = string.IsNullOrWhiteSpace(query.FolderName) ? "inbox" : query.FolderName;
        var url = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/mailFolders/{Encode(folder)}/messages"
            + "?$select=id,internetMessageId,subject,from,receivedDateTime,hasAttachments"
            + "&$filter=" + Uri.EscapeDataString(filter)
            + "&$orderby=receivedDateTime asc"
            + "&$top=" + query.MaxMessages;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(credentials, request, ct);
        await EnsureSuccessAsync(response, "message listing", ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var results = new List<MailboxMessage>();
        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var subject = item.TryGetProperty("subject", out var s) ? s.GetString() ?? string.Empty : string.Empty;

            if (!string.IsNullOrWhiteSpace(query.SubjectContains)
                && !subject.Contains(query.SubjectContains, StringComparison.OrdinalIgnoreCase))
                continue;

            // Cheap skip so we never issue an attachments request for a message that has none.
            if (item.TryGetProperty("hasAttachments", out var ha)
                && ha.ValueKind == JsonValueKind.False)
                continue;

            var id = item.GetProperty("id").GetString()!;
            var internetMessageId = item.TryGetProperty("internetMessageId", out var imid)
                ? imid.GetString() ?? id
                : id;
            var received = item.TryGetProperty("receivedDateTime", out var r) && r.TryGetDateTimeOffset(out var dto)
                ? dto.UtcDateTime
                : DateTime.UtcNow;
            var from = item.TryGetProperty("from", out var f)
                && f.ValueKind == JsonValueKind.Object
                && f.TryGetProperty("emailAddress", out var ea)
                && ea.TryGetProperty("address", out var addr)
                    ? addr.GetString() ?? string.Empty
                    : string.Empty;

            if (!string.IsNullOrWhiteSpace(query.FromAddress)
                && !string.Equals(from, query.FromAddress.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new MailboxMessage(id, internetMessageId, subject, from, received, Array.Empty<MailboxAttachment>()));
        }

        // Oldest first, so a backlog is ingested in chronological order and history context builds up correctly.
        return results.OrderBy(m => m.ReceivedAtUtc).ToList();
    }

    /// <summary>
    /// Downloads the PDF attachments for one message. Kept separate from listing so a poll
    /// that matches nothing never pays to transfer attachment bytes.
    /// </summary>
    public async Task<IReadOnlyList<MailboxAttachment>> FetchPdfAttachmentsAsync(
        MailboxCredentials credentials, string messageId, string? nameContains, CancellationToken ct = default)
    {
        var listUrl = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/messages/{Encode(messageId)}/attachments"
            + "?$select=id,name,contentType,size";

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, listUrl);
        using var listResponse = await SendAsync(credentials, listRequest, ct);
        await EnsureSuccessAsync(listResponse, "attachment listing", ct);

        using var doc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(ct));

        var attachments = new List<MailboxAttachment>();
        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var odataType = item.TryGetProperty("@odata.type", out var t) ? t.GetString() : null;
            if (odataType is not null && !odataType.Contains("fileAttachment", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var contentType = item.TryGetProperty("contentType", out var c) ? c.GetString() ?? string.Empty : string.Empty;

            var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (!isPdf) continue;

            if (!string.IsNullOrWhiteSpace(nameContains)
                && !name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                continue;

            var attachmentId = item.GetProperty("id").GetString()!;

            // $value streams the raw bytes and works above the ~3 MB inline contentBytes
            // threshold, so it is used for every size.
            var valueUrl = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/messages/{Encode(messageId)}"
                + $"/attachments/{Encode(attachmentId)}/$value";

            using var valueRequest = new HttpRequestMessage(HttpMethod.Get, valueUrl);
            using var valueResponse = await SendAsync(credentials, valueRequest, ct);
            await EnsureSuccessAsync(valueResponse, "attachment download", ct);

            var bytes = await valueResponse.Content.ReadAsByteArrayAsync(ct);
            attachments.Add(new MailboxAttachment(name, bytes));
        }

        return attachments;
    }

    public async Task MarkAsReadAsync(MailboxCredentials credentials, string messageId, CancellationToken ct = default)
    {
        var url = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/messages/{Encode(messageId)}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent("{\"isRead\":true}", Encoding.UTF8, "application/json")
        };
        using var response = await SendAsync(credentials, request, ct);
        await EnsureSuccessAsync(response, "mark as read", ct);
    }

    public async Task MoveAsync(
        MailboxCredentials credentials, string messageId, string destinationFolder, CancellationToken ct = default)
    {
        var url = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/messages/{Encode(messageId)}/move";
        var payload = JsonSerializer.Serialize(new { destinationId = destinationFolder });
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        using var response = await SendAsync(credentials, request, ct);
        await EnsureSuccessAsync(response, "message move", ct);
    }

    public async Task SendMailAsync(
        MailboxCredentials credentials,
        IReadOnlyList<string> to,
        string subject,
        string htmlBody,
        IReadOnlyList<MailboxAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        if (to.Count == 0)
            throw new MailboxException(MailboxFailureKind.Configuration, "No summary recipients are configured.");

        var message = new Dictionary<string, object?>
        {
            ["subject"] = subject,
            ["body"] = new { contentType = "HTML", content = htmlBody },
            ["toRecipients"] = to.Select(a => new { emailAddress = new { address = a } }).ToArray()
        };

        if (attachments is { Count: > 0 })
        {
            message["attachments"] = attachments.Select(a => new Dictionary<string, object?>
            {
                ["@odata.type"] = "#microsoft.graph.fileAttachment",
                ["name"] = a.Name,
                ["contentBytes"] = Convert.ToBase64String(a.Content)
            }).ToArray();
        }

        var payload = JsonSerializer.Serialize(new { message, saveToSentItems = true });
        var url = $"{GraphBase}/users/{Encode(credentials.MailboxAddress)}/sendMail";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        using var response = await SendAsync(credentials, request, ct);
        await EnsureSuccessAsync(response, "send mail", ct);
    }

    private static string Encode(string segment) => Uri.EscapeDataString(segment);
}
