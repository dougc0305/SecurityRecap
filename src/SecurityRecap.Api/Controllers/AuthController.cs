using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<LoginResponse>.Fail("Invalid email or password"));

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(ApiResponse<LoginResponse>.Fail("Invalid email or password"));

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        var response = new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            User: new UserInfo(user.Id, user.Email!, user.FullName, user.Role.ToString(), user.TenantId)
        );

        return Ok(ApiResponse<LoginResponse>.Ok(response));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshRequest request)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken
                && rt.ExpiresAt > DateTime.UtcNow
                && !rt.IsRevoked);

        if (storedToken is null)
            return Unauthorized(ApiResponse<LoginResponse>.Fail("Invalid or expired refresh token"));

        // Revoke the used token (rotation)
        storedToken.IsRevoked = true;

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<LoginResponse>.Fail("User not found or inactive"));

        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        var response = new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            User: new UserInfo(user.Id, user.Email!, user.FullName, user.Role.ToString(), user.TenantId)
        );

        return Ok(ApiResponse<LoginResponse>.Ok(response));
    }
}
