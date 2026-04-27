// =============================================================================
// Notification.API — Program.cs
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Bu servis "Consumer-Only" bir mikroservistir.
// HTTP endpoint'i yok veya çok azdır.
// Rolü: RabbitMQ'dan event'leri dinleyip bildirim göndermek.
//
// MassTransit Consumer Registrasyonu:
// → MassTransit, consumer sınıflarını otomatik bulur ve queue'lara bağlar.
//   Her consumer, bir event tipini dinler.
//   Queue adı otomatik oluşturulur: "notification-[event-type]"
//
// NEDEN Ayrı Notification Servisi?
// → E-posta gönderimi yavaştır (ağ I/O). Ana işlem akışını bloke etmemeli.
// → Farklı bildirim kanalları eklenebilir (SMS, push notification, WebSocket).
// → Rate limiting uygulanabilir (spam önleme).
// =============================================================================

using MassTransit;
using Notification.API.Consumers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("ServiceName", "Notification.API")
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

    // =========================================================================
    // MassTransit Consumer Registrasyonu
    // =========================================================================
    builder.Services.AddMassTransit(cfg =>
    {
        // ─────────────────────────────────────────────────────
        // AddConsumer: Her consumer bir event tipini dinler
        // MassTransit otomatik olarak:
        // 1. RabbitMQ'da exchange oluşturur (event tipi adında)
        // 2. Queue oluşturur (consumer tipi adında)
        // 3. Exchange → Queue binding yapar
        // 4. Mesaj geldiğinde consumer'ın Consume metodunu çağırır
        // ─────────────────────────────────────────────────────
        cfg.AddConsumer<UserRegisteredConsumer>();
        cfg.AddConsumer<BookCreatedConsumer>();
        cfg.AddConsumer<BookBorrowedConsumer>();
        cfg.AddConsumer<BookReturnedConsumer>();

        cfg.UsingRabbitMq((context, busConfig) =>
        {
            // ─────────────────────────────────────────────────────
            // Aspire Connection String Desteği
            // Aspire "rabbitmq" kaynağı: ConnectionStrings__rabbitmq env varı inject edilir
            // Standalone: appsettings.json'dan RabbitMQ:Host okunur
            // ─────────────────────────────────────────────────────
            var rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
            if (!string.IsNullOrEmpty(rabbitConnectionString))
            {
                busConfig.Host(new Uri(rabbitConnectionString));
            }
            else
            {
                busConfig.Host(
                    builder.Configuration["RabbitMQ:Host"] ?? "localhost",
                    builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
                    h =>
                    {
                        h.Username(builder.Configuration["RabbitMQ:Username"] ?? "lms_user");
                        h.Password(builder.Configuration["RabbitMQ:Password"] ?? "lms_password_2024");
                    });
            }

            // ─────────────────────────────────────────────────
            // Retry Policy:
            // → Consumer hata alırsa 3 kez dener.
            //   3 denemeden sonra da hata alırsa mesaj
            //   Dead Letter Queue'ya (DLQ) taşınır.
            //   DLQ'daki mesajlar incelenip tekrar işlenebilir.
            // ─────────────────────────────────────────────────
            busConfig.UseMessageRetry(retryConfig =>
                retryConfig.Interval(3, TimeSpan.FromSeconds(5)));

            busConfig.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // ASPIRE: Health check endpoint'leri (/health + /alive)
    // Manuel /health endpoint'i kaldırıldı — MapDefaultEndpoints() bunu yönetir
    app.MapDefaultEndpoints();

    Log.Information("Notification.API başarıyla başlatıldı. Event consumer aktif.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notification.API başlatılırken kritik hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}

// WebApplicationFactory icin gerekli — smoke testlerin Program sinifina erisimini saglar
public partial class Program { }
