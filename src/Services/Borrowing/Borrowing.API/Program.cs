// =============================================================================
// Borrowing.API — Program.cs
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Borrowing Servisi Sorumlulukları:
// → Kitap ödünç alma ve iade işlemlerini yönetir
// → Catalog servisindeki kitap stok bilgisini event'ler ile takip eder
// → BookBorrowed/BookReturned event'lerini publish eder
//
// Polly Resiliency Patterns:
// → Retry: Geçici hatalarda otomatik yeniden deneme
// → Circuit Breaker: Sürekli hatalarda devre kesici (cascade failure önleme)
//
// ┌────────────────────────────────────────────────────────────────────┐
// │ CIRCUIT BREAKER DURUMLARI:                                         │
// │                                                                    │
// │ Closed (Normal)  → İstekler geçer, hatalar sayılır                │
// │ Open (Devre Açık) → İstekler engellenir, bekleme süresi başlar    │
// │ Half-Open        → Deneme isteği gönderilir, başarılıysa Closed'a │
// │                     döner, başarısızsa Open'a kalır               │
// └────────────────────────────────────────────────────────────────────┘
// =============================================================================

using Borrowing.API.Data;
using Borrowing.API.Endpoints;
using EventBus.RabbitMQ;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("ServiceName", "Borrowing.API")
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // EF Core + PostgreSQL
    builder.Services.AddDbContext<BorrowingDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("BorrowingDb")
            ?? "Host=localhost;Database=lms_borrowing_db;Username=lms_user;Password=lms_password_2024"));

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // =========================================================================
    // Polly Resiliency — HttpClient için Retry + Circuit Breaker
    // =========================================================================
    // EĞİTİCİ NOT:
    // NEDEN Polly?
    // → Microservice mimarisinde servisler birbirine HTTP ile istek atar.
    //   Network geçici olarak kesildiğinde veya hedef servis geçici olarak
    //   yanıt vermediğinde retry ile otomatik tekrar dener.
    //   Sürekli hata durumunda ise Circuit Breaker devreyi keser ve
    //   cascade failure'ı (domino etkisini) önler.
    //
    // Exponential Backoff:
    // → Her retry arasında bekleme süresi katlanarak artar:
    //   1. deneme: 2^1 = 2 saniye
    //   2. deneme: 2^2 = 4 saniye
    //   3. deneme: 2^3 = 8 saniye
    // =========================================================================
    builder.Services.AddHttpClient("CatalogService", client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Services:CatalogUrl"] ?? "http://localhost:5001");
    });

    // RabbitMQ Event Bus
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
    app.MapBorrowingEndpoints();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();
        await db.Database.MigrateAsync();
    }

    Log.Information("Borrowing.API başarıyla başlatıldı.");
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
    Log.Fatal(ex, "Borrowing.API başlatılırken kritik hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}
