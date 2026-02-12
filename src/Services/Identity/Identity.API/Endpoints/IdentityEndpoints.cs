// =============================================================================
// IdentityEndpoints — Kimlik Doğrulama Endpoint'leri
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Register + Login akışı:
// 1. POST /register → Yeni kullanıcı oluştur → UserRegisteredEvent publish et
// 2. POST /login → Email + Password doğrula → JWT Token döndür
// 3. Client, sonraki isteklerde Authorization: Bearer <token> gönderir
// 4. Diğer servisler JWT'yi doğrulayarak kullanıcıyı tanır
//
// JWT Token İçeriği (Claims):
// → sub: Kullanıcı ID
// → email: Email adresi
// → name: Tam ad
// → role: Kullanıcı rolü (User, Librarian, Admin)
// → exp: Token son kullanım tarihi
// =============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventBus.Abstractions;
using EventBus.Abstractions.Events;
using Identity.API.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Endpoints;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/identity")
            .WithTags("Identity");

        // =====================================================================
        // POST /register — Kullanıcı Kayıt
        // =====================================================================
        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            IEventBus eventBus) =>
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return Results.BadRequest(new { Errors = errors });
            }

            // Role atama
            await userManager.AddToRoleAsync(user, "User");

            // ─────────────────────────────────────────────────
            // UserRegisteredIntegrationEvent publish
            // → Notification servisi bu event'i consume eder
            //   ve hoşgeldin e-postası gönderir.
            // ─────────────────────────────────────────────────
            await eventBus.PublishAsync(new UserRegisteredIntegrationEvent(
                user.Id, user.FullName, user.Email!));

            return Results.Created($"/api/v1/identity/{user.Id}",
                new { user.Id, user.Email, user.FullName });
        })
        .WithName("Register")
        .WithSummary("Yeni kullanıcı kaydı oluşturur");

        // =====================================================================
        // POST /login — Kullanıcı Giriş (JWT Token Üretimi)
        // =====================================================================
        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            if (!user.IsActive)
                return Results.Forbid();

            // ─────────────────────────────────────────────────
            // JWT Token Oluşturma
            // ─────────────────────────────────────────────────
            var roles = await userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Name, user.FullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Rolleri claim olarak ekle
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                configuration["Jwt:Key"] ?? "LMS-Secret-Key-2024-Minimum-32-Characters-Long!!"));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"] ?? "LMS.Identity",
                audience: configuration["Jwt:Issuer"] ?? "LMS.Identity",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new
            {
                Token = tokenString,
                Expiration = token.ValidTo,
                user.FullName,
                Roles = roles
            });
        })
        .WithName("Login")
        .WithSummary("Kullanıcı girişi yapar ve JWT token döndürür");
    }
}

// ─────────────────────────────────────────────────────────
// Request DTO'ları
// ─────────────────────────────────────────────────────────
public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);

// ─────────────────────────────────────────────────────────
// UserRegisteredIntegrationEvent
// → Notification servisi tarafından consume edilir
// ─────────────────────────────────────────────────────────
public record UserRegisteredIntegrationEvent(
    string UserId, string FullName, string Email) : IntegrationEvent;
