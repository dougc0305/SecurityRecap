using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecurityRecap.Api.Auth;
using SecurityRecap.Api.Services;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;
using SecurityRecap.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Data Protection — protects integration secrets stored in the database (the Graph client
// secret). The key ring MUST live outside the deploy root: deploy.bat moves that directory
// wholesale on every release, which would otherwise silently leave every stored secret
// undecryptable and require re-entering it after each deploy.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("SecurityRecap");
var keyRingPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    Directory.CreateDirectory(keyRingPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

    // Without this the key ring is plaintext XML on disk, so anything that can read the
    // folder can decrypt every stored integration secret. Machine-scoped DPAPI survives an
    // app pool identity change and needs no loaded user profile, unlike the user-scoped
    // default. Existing keys stay readable; only newly created ones are encrypted.
    if (OperatingSystem.IsWindows())
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}
else if (!builder.Environment.IsDevelopment())
{
    Console.Error.WriteLine(
        "WARNING: DataProtection:KeyPath is not configured. Stored mailbox client secrets will "
        + "become undecryptable when the deploy replaces the application directory.");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (builder.Environment.IsDevelopment())
        jwtSecret = "CHANGE_ME_IN_PRODUCTION_MIN_32_CHARS!!";
    else
        throw new InvalidOperationException("Jwt:Secret must be configured.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
                ? ApiKeyAuthenticationHandler.SchemeName
                : null;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SecurityRecap",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SecurityRecap",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(nameof(UserRole.Admin)));

    options.AddPolicy("OperationalUser", policy =>
        policy.RequireRole(
            nameof(UserRole.Admin),
            nameof(UserRole.Manager)));
});

// Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();
builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<ReportHistoryContextBuilder>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

// Automated report pickup from a mailbox
builder.Services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddHttpClient<IMailboxClient, GraphMailboxClient>(client =>
{
    // Report PDFs run to a few MB and Graph can be slow to hand them over.
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<MailboxAlertNotifier>();
builder.Services.AddScoped<IMailboxIngestionService, MailboxIngestionService>();
builder.Services.AddHostedService<MailboxPollingBackgroundService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IAddressOfInterestService, AddressOfInterestService>();

builder.Services.AddScoped<IChatService, ChatService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
