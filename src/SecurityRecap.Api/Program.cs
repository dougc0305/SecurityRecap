using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecurityRecap.Api.Services;
using SecurityRecap.Core.Entities;
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
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "CHANGE_ME_IN_PRODUCTION_MIN_32_CHARS!!";
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SecurityRecap",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SecurityRecap",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();

builder.Services.AddScoped<IChatService, ChatService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Controllers
builder.Services.AddControllers();
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
