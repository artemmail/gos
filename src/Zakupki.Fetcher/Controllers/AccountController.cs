using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Vkontakte;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly NoticeDbContext _dbContext;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        NoticeDbContext dbContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("signin-google")]
    public Task<IActionResult> SignInWithGoogle(string? returnUrl = null)
        => SignInWithProviderAsync("Google", returnUrl);

    [AllowAnonymous]
    [HttpGet("signin-yandex")]
    public Task<IActionResult> SignInWithYandex(string? returnUrl = null)
        => SignInWithProviderAsync("Yandex", returnUrl);

    [AllowAnonymous]
    [HttpGet("signin-vkontakte")]
    public Task<IActionResult> SignInWithVkontakte(string? returnUrl = null)
        => SignInWithProviderAsync(VkontakteAuthenticationDefaults.AuthenticationScheme, returnUrl);

    [AllowAnonymous]
    [HttpGet("externallogincallback")]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            return RedirectToAngularWithError(remoteError);
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return RedirectToAngularWithError("NoExternalLoginInfo");
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

        ApplicationUser user;
        if (signInResult.Succeeded)
        {
            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey)
                ?? throw new InvalidOperationException("User not found for existing external login.");
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                        ?? info.Principal.FindFirstValue("urn:yandex:email");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAngularWithError("EmailNotFound");
            }

            user = await _userManager.FindByEmailAsync(email);
            if (user is null)
            {
                var displayName = info.Principal.FindFirstValue(ClaimTypes.Name)
                                 ?? info.Principal.FindFirstValue("urn:yandex:login")
                                 ?? GenerateDefaultDisplayName();

                user = new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    DisplayName = displayName
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return RedirectToAngularWithError("UserCreationFailed");
                }

                user = await _userManager.FindByEmailAsync(email)
                    ?? throw new InvalidOperationException("User was created but not found afterwards.");
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return RedirectToAngularWithError("ExternalLoginFailed");
            }

            if (!await _userManager.IsInRoleAsync(user, "Free"))
            {
                await _userManager.AddToRoleAsync(user, "Free");
            }
        }

        await EnsureDisplayNameAsync(user);

        var accessToken = await GenerateJwtToken(user);
        var refreshTokenValue = GenerateRefreshToken();
        var refreshLifetime = TimeSpan.FromDays(30);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenValue,
            UserId = user.Id,
            Created = DateTime.UtcNow,
            Expires = DateTime.UtcNow + refreshLifetime
        });
        await _dbContext.SaveChangesAsync();

        Response.Cookies.Append("refreshToken", refreshTokenValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow + refreshLifetime
        });

        var redirect = $"{GetAngularRedirectUrl()}?token={accessToken}";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            redirect += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return Redirect(redirect);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshTokenValue = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshTokenValue))
        {
            var tokenEntity = await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue);
            if (tokenEntity is not null)
            {
                tokenEntity.IsRevoked = true;
                await _dbContext.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    [HttpGet("profile")]
    [AllowAnonymous]
    public async Task<ActionResult<UserProfileDto?>> GetProfile()
    {
        if (User?.Identity is null || !User.Identity.IsAuthenticated)
        {
            return Ok(null);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(null);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Ok(null);
        }

        await EnsureDisplayNameAsync(user);

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName
        });
    }

    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest("Display name is required.");
        }

        user.DisplayName = displayName;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update profile.");
        }

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName
        });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshTokenValue = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshTokenValue))
        {
            return Unauthorized("No refresh token");
        }

        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue && rt.Expires > DateTime.UtcNow && !rt.IsRevoked);

        if (tokenEntity is null)
        {
            return Unauthorized("Invalid refresh token");
        }

        var user = await _userManager.FindByIdAsync(tokenEntity.UserId);
        if (user is null)
        {
            return Unauthorized("User not found");
        }

        await EnsureDisplayNameAsync(user);

        tokenEntity.IsRevoked = true;

        var newAccessToken = await GenerateJwtToken(user);
        var newRefreshTokenValue = GenerateRefreshToken();
        var refreshLifetime = TimeSpan.FromDays(30);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = newRefreshTokenValue,
            UserId = user.Id,
            Created = DateTime.UtcNow,
            Expires = DateTime.UtcNow + refreshLifetime
        });
        await _dbContext.SaveChangesAsync();

        Response.Cookies.Append("refreshToken", newRefreshTokenValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow + refreshLifetime
        });

        return Ok(new { token = newAccessToken });
    }

    private async Task<IActionResult> SignInWithProviderAsync(string provider, string? returnUrl)
    {
        var availableSchemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        var hasProvider = availableSchemes.Any(scheme =>
            string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase));

        if (!hasProvider)
        {
            return RedirectToAngularWithError("ProviderUnavailable");
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    private IActionResult RedirectToAngularWithError(string errorCode)
    {
        var redirect = $"{GetAngularRedirectUrl()}?error={Uri.EscapeDataString(errorCode)}";
        return Redirect(redirect);
    }

    private static string GenerateRefreshToken()
        => $"{Guid.NewGuid():N}{Guid.NewGuid():N}";

    private string GetAngularRedirectUrl()
        => _configuration["Angular:RedirectUri"] ?? "/auth/callback";

    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var key = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        {
            KeyId = _configuration["Jwt:KeyId"]
        };

        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.DisplayName ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName ?? string.Empty),
            new("displayName", user.DisplayName ?? string.Empty),
            new("lifetimeAccess", user.HasLifetimeAccess.ToString())
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expireMinutes = _configuration.GetValue<int?>("Jwt:ExpireMinutes") ?? 60;

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task EnsureDisplayNameAsync(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return;
        }

        user.DisplayName = GenerateDefaultDisplayName();
        await _userManager.UpdateAsync(user);
    }

    private static string GenerateDefaultDisplayName()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return $"User{number:000000}";
    }

    public sealed class UpdateProfileRequest
    {
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
