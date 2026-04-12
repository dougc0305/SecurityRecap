using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

public record CreatePropertyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(2)] string State,
    [Required, MaxLength(10)] string Zip,
    string? SecurityCompany,
    [EmailAddress] string? ReportEmail,
    string Timezone = "America/New_York"
);

public record UpdatePropertyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(2)] string State,
    [Required, MaxLength(10)] string Zip,
    string? SecurityCompany,
    [EmailAddress] string? ReportEmail,
    string Timezone = "America/New_York",
    bool IsActive = true
);

public record PropertyResponse(
    Guid Id,
    string Name,
    string Address,
    string City,
    string State,
    string Zip,
    string? SecurityCompany,
    string? ReportEmail,
    string Timezone,
    bool IsActive,
    DateTime CreatedAt
);
