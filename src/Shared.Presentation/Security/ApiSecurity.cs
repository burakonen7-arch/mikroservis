using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Shared.Presentation.Security;

public static class ApiSecurity
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "mikroservis";
        var audience = configuration["Jwt:Audience"] ?? "mikroservis-api";
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be supplied as a secret of at least 32 bytes. Run scripts/prepare-debug.ps1 for local development.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub",
                RoleClaimType = "role",
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(ApiPolicies.ProductWrite, policy => policy.RequireRole("admin", "catalog"));
            options.AddPolicy(ApiPolicies.StockWrite, policy => policy.RequireRole("admin", "inventory"));
        });
        
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 120);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        var loginPermitLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
        if (permitLimit < 1 || windowSeconds < 1 || loginPermitLimit < 1)
            throw new InvalidOperationException("Rate limiting limits and window must be positive.");
        
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(ApiPolicies.Requests, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            
            options.AddPolicy(ApiPolicies.Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            
            // 1. API'ye gelen istek için "Requests" rate limit politikası uygulanır.
            // 2. JWT'deki "sub" (User ID) alınır. Yoksa IP adresi kullanılır.
            // 3. Kullanıcı/IP bazında ayrı bir istek limiti takip edilir.
            // 4. PermitLimit → Belirlenen sürede yapılabilecek maksimum istek sayısı.
            // 5. Window → Limitin geçerli olduğu zaman aralığı (örnek: 60 saniye).
            // 6. QueueLimit = 0 → Limit aşılırsa istek bekletilmeden reddedilir.
            // 7. AutoReplenishment = true → Süre dolunca istek hakkı otomatik yenilenir.
            //
            // Örnek: 60 saniyede 100 istek hakkı varsa 101. istek reddedilir.
            // Yeni zaman penceresi başladığında tekrar 100 istek hakkı tanımlanır.
            
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
             
                await Results.Problem(statusCode: 429, title: "Too many requests", detail: "Wait for the Retry-After interval before sending another request.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }

    public static IServiceCollection AddApiSwagger(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });
            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Obtain a development token from the Gateway POST /auth/token endpoint, then enter the access token here."
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
        });
        return services;
    }

    public static IApplicationBuilder UseApiSecurity(this IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }
}
