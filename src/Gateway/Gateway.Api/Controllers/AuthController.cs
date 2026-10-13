using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Shared.Presentation.Security;

namespace Gateway.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiPolicies.Login)]
    public IActionResult Token([FromBody] LoginRequest request)
    {
        // This local credential issuer is deliberately unavailable in production.
        if (!environment.IsDevelopment()) return NotFound();
        var role = request.Username switch
        {
            "admin" => "admin",
            "reader" => "reader",
            "inventory" => "inventory",
            "catalog" => "catalog",
            _ => null
        };
        
        var expected = role is null ? null : configuration[$"DevelopmentAuth:{role}:Password"];
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected ?? string.Empty));
        
        if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            return Unauthorized();

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(15);
        
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "mikroservis",
            audience: configuration["Jwt:Audience"] ?? "mikroservis-api",
            claims: [new Claim("sub", request.Username), new Claim("role", role!), new Claim("jti", Guid.NewGuid().ToString())],
            notBefore: now, expires: expires, signingCredentials: credentials);
        
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresIn = 900 });
    }
}

public sealed record LoginRequest(string Username, string Password);
