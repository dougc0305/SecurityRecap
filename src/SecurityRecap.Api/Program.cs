using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecurityRecap.Api.Services;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;
using SecurityRecap.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(nameof(UserRole.Admin)));

    options.AddPolicy("OperationalUser", policy =>
        policy.RequireRole(
            nameof(UserRole.Admin),
            nameof(UserRole.PropertyManager),
            nameof(UserRole.SecurityPersonnel)));
});

// Services
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();
builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<IIngestionService, IngestionService>();
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
