// =============================================================================
// Identity.API — Program.cs
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// JWT Authentication Akışı:
// ┌──────────┐    ┌──────────────┐    ┌───────────────┐
// │ Client   │ → │ POST /login  │ → │ JWT Token     │
// │          │ ← │              │ ← │ oluşturulur   │
// └──────────┘    └──────────────┘    └───────────────┘
//       │
//       │ Authorization: Bearer <token>
//       ▼
// ┌──────────────┐    ┌──────────────────┐
// │ Catalog API  │ → │ Token doğrulanır │
// │              │    │ (aynı secret key) │
// └──────────────┘    └──────────────────┘
//
// NEDEN JWT?
// → Stateless: Sunucuda session tutulmaz, her servis token'ı bağımsız doğrular
// → Scalable: Load balancer arkasında sorunsuz çalışır
// → Claims-based: Kullanıcı bilgileri (rol, id) token içinde taşınır
//
// ALTERNATİF:
// → OAuth2 + IdentityServer/Duende: Daha kapsamlı, third-party login desteği
// → API Key: Basit ama kullanıcı kimliği taşımaz
// =============================================================================

using System.Text;
using EventBus.RabbitMQ;
using Identity.API.Data;
using Identity.API.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("ServiceName", "Identity.API")
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ==========================================================================
    // ASPIRE: AddServiceDefaults
    // ==========================================================================
    // 📚 EĞİTİCİ NOT: OpenTelemetry + Health Checks + Service Discovery tek satırda.
    // ==========================================================================
    builder.AddServiceDefaults();

    // =========================================================================
    // 1. EF Core + PostgreSQL — Aspire Yönetimli
    // =========================================================================
    // "identity-db" → AppHost'ta: postgres.AddDatabase("identity-db")
    // AddNpgsqlDbContext: Connection string otomatik okunur + health check eklenir
    // =========================================================================
    builder.AddNpgsqlDbContext<IdentityDbContext>("identity-db");

    // =========================================================================
    // 2. ASP.NET Core Identity
    // =========================================================================
    // EĞİTİCİ NOT:
    // AddIdentity: UserManager, SignInManager, RoleManager gibi servisleri kaydeder.
    // Password policy, lockout kuralları burada konfigüre edilir.
    // =========================================================================
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

    // =========================================================================
    // 3. JWT Authentication
    // =========================================================================
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "LMS-Secret-Key-2024-Minimum-32-Characters-Long!!";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LMS.Identity";

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            // ─────────────────────────────────────────────────
            // SymmetricSecurityKey: Aynı key ile şifreleme/doğrulama
            // Production'da AsymmetricSecurityKey (RSA) kullanılmalıdır:
            // → Identity servisi private key ile imzalar
            // → Diğer servisler public key ile doğrular
            // ─────────────────────────────────────────────────
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

    builder.Services.AddAuthorization();

    // =========================================================================
    // 4. MediatR + RabbitMQ
    // =========================================================================
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    builder.Services.AddRabbitMqEventBus(builder.Configuration);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapIdentityEndpoints();

    // ASPIRE: Health check endpoint'leri (/health + /alive)
    app.MapDefaultEndpoints();

    // EF Core migration'ları otomatik uygula (development ortamında)
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync();

        // =====================================================================
        // Rol Seed — Varsayılan rolleri oluştur
        // =====================================================================
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = ["User", "Librarian", "Admin"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Log.Information("Rol oluşturuldu: {Role}", role);
            }
        }
    }

    Log.Information("Identity.API başarıyla başlatıldı.");
    app.Run();
}
catch (HostAbortedException)
{
    // EF Core design-time tooling tarafından fırlatılır (dotnet ef migrations vb.)
    // Bu beklenen bir davranıştır, fatal error olarak loglanmamalıdır.
    Log.Information("Host, EF Core design-time tooling tarafından durduruldu.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity.API başlatılırken kritik hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}
