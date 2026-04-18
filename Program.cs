// =============================================================================
// API Gateway — Program.cs (YARP Reverse Proxy)
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// API Gateway Design Pattern (Microservices):
// → Tüm client istekleri tek bir noktadan geçer.
//   Gateway, istekleri doğru servise yönlendirir.
//
// YARP (Yet Another Reverse Proxy):
// → Microsoft'un resmi .NET proxy kütüphanesi.
// → Konfigürasyon tabanlı routing (appsettings.json).
// → Hot reload: Uygulama yeniden başlatılmadan route değişikliği.
//
// Gateway Katmanında Yapılanlar:
// 1. JWT Authentication → Token geçersizse istek reddedilir
// 2. Rate Limiting → DDoS/Brute Force koruması
// 3. CORS → Frontend framework'lerinin erişim kuralları
// 4. Request Routing → /api/catalog/* → Catalog Service
//
// NEDEN Gateway Seviyesinde JWT?
// → Her serviste ayrı ayrı JWT doğrulama yapmak:
//   - Kod tekrarı (DRY ihlali)
//   - Konfigürasyon tutarsızlığı riski
//   Gateway'de merkezi JWT doğrulaması yapılır.
//   Servislere sadece doğrulanmış istekler ulaşır.
//
// Rate Limiting (.NET 8 Built-in):
// → Fixed Window: Belirli sürede sabit istek sayısı
// → Sliding Window: Kayan pencere ile daha doğru limit
// → Token Bucket: Burst isteklere izin verir
// → Concurrency: Eşzamanlı istek sayısı sınırı
// =============================================================================

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using System.Timers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("ServiceName", "ApiGateway")
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ==========================================================================
    // ASPIRE: AddServiceDefaults
    // ==========================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // AddServiceDefaults() burada da çalışır:
    // → OpenTelemetry: Tüm Gateway istekleri trace edilir
    // → Service Discovery: YARP'la birlikte "http://catalog-api" gibi
    //   Aspire adları gerçek adreslere çevrilir
    // → Health Checks: /health + /alive endpoint'leri eklenir
    // ==========================================================================
    builder.AddServiceDefaults();

    // =========================================================================
    // 1. YARP Reverse Proxy + Aspire Service Discovery
    // =========================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // .AddServiceDiscoveryDestinationResolver() ne yapar?
    // → YARP, cluster destination adreslerini çözerken
    //   Microsoft.Extensions.ServiceDiscovery'yi kullanır.
    //
    // appsettings.json'da:
    //   "Address": "http://catalog-api"   ← Aspire servis adı
    //   YARP bunu runtime'da gerçek IP:PORT adresine çevirir.
    //
    // Eski yaklaşım:
    //   "Address": "https://localhost:65173"  ← Hard-coded port
    //   Her geliştirici farklı port kullanabilir, CI/CD'de değişir.
    // =========================================================================
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddServiceDiscoveryDestinationResolver();

    // =========================================================================
    // 2. JWT Authentication (Gateway Level)
    // =========================================================================
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? "LMS-Secret-Key-2024-Minimum-32-Characters-Long!!";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "LMS.Identity",
                ValidAudience = "LMS.Identity",
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };
        });

    builder.Services.AddAuthorization();

    // =========================================================================
    // 3. Rate Limiting (.NET 8 Built-in)
    // =========================================================================
    // EĞİTİCİ NOT:
    // NEDEN Rate Limiting?
    // → API'ye aşırı istek gönderilmesini engeller.
    //   DDoS saldırıları, brute force giriş denemeleri,
    //   ve API abuse'u önler.
    //
    // Fixed Window Limiter:
    // → 10 saniyelik pencerede en fazla 100 istek kabul edilir.
    //   Pencere dolunca yeni istekler 429 Too Many Requests alır.
    //   Queue'da bekleyen istekler otomatik sıraya alınır.
    // =========================================================================
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromSeconds(10),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));
    });

    // =========================================================================
    // 4. CORS
    //Bu kod, sadece http://localhost:5173 adresinden gelen isteklerin API Gateway’e erişmesine izin verir.
    //Frontend uygulamanız bu portlarda çalışıyorsa, API’ye erişim sorunsuz olur.
    //Diğer adreslerden gelen istekler CORS nedeniyle reddedilir.
    // =========================================================================
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "LMS API Gateway",
            Version = "v1",
            Description = "Library Management System — API Gateway (YARP Reverse Proxy)"
        });
    });

    // =========================================================================
    // MIDDLEWARE PIPELINE
    // =========================================================================
    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("AllowFrontend");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // ─────────────────────────────────────────────────────
    // YARP Reverse Proxy middleware'ini ekle
    // → Bu middleware, gelen istekleri appsettings.json'daki
    //   route tanımlarına göre doğru servise yönlendirir.
    // ─────────────────────────────────────────────────────
    app.MapReverseProxy();

    // ==========================================================================
    // ASPIRE: Health check endpoint'leri (/health + /alive)
    // ==========================================================================
    // 📚 EĞİTİCİ NOT:
    // Manuel /health endpoint'i kaldırıldı.
    // MapDefaultEndpoints() hem /health hem /alive endpoint'ini yönetir.
    // Aspire Dashboard bu endpoint'leri kullanarak servis durumunu takip eder.
    // ==========================================================================
    app.MapDefaultEndpoints();

    Log.Information("API Gateway başarıyla başlatıldı.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway başlatılırken kritik hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}
