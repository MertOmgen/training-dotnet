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

    // ==========================================================================
    // ASPIRE: AddServiceDefaults
    // ==========================================================================
    builder.AddServiceDefaults();

    // EF Core + PostgreSQL — Aspire Yönetimli
    // "borrowing-db" → AppHost'ta: postgres.AddDatabase("borrowing-db")
    builder.AddNpgsqlDbContext<BorrowingDbContext>("borrowing-db");

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // =========================================================================
    // Polly Resiliency — HttpClient için Retry + Circuit Breaker
    // =========================================================================
    // 📚 EĞİTİCİ NOT (Tech-Tutor):
    //
    // AddServiceDefaults() → ConfigureHttpClientDefaults() → AddStandardResilienceHandler()
    // BUNU otomatik yapar (tüm HttpClient'lar için).
    //
    // Aspire Service Discovery ile "CatalogService" URL'i artık:
    //   Eski: "http://localhost:5001" (sabit port)
    //   Yeni: "http://catalog-api"    (Aspire DNS çözümler)
    //
    // Aspire bu DNS adını çalışma zamanında gerçek container adresine çevirir.
    // =========================================================================
    builder.Services.AddHttpClient("CatalogService", client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Services:CatalogUrl"] ?? "http://catalog-api");
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

    // ASPIRE: Health check endpoint'leri (/health + /alive)
    app.MapDefaultEndpoints();

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
