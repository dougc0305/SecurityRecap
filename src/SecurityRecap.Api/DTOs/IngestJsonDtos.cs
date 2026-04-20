using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

public record IngestReportJsonRequest(
    [Required] Guid PropertyId,
    [Required] string FileName,
    [Required] string PdfBase64,
    string? ExternalId);

public record IngestReportResponse(Guid ReportId, bool AlreadyIngested);
