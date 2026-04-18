// =============================================================================
// RabbitMQ ve MassTransit DI Registrasyonu
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Extension Method?
// → Program.cs dosyasını temiz tutmak için DI registrasyonlarını
//   extension method'lara ayırırız. Bu, Clean Code prensibidir.
//
// NASIL Kullanılır?
// → Program.cs'te: builder.Services.AddRabbitMqEventBus(builder.Configuration);
//
// MassTransit Konfigürasyonu:
// → Host: RabbitMQ bağlantı bilgileri (appsettings.json'dan okunur)
// → ConfigureEndpoints: Consumer'ları otomatik keşfeder ve queue oluşturur
// → Retry: Hatalı mesajlar için yeniden deneme politikası
// =============================================================================

using EventBus.Abstractions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.RabbitMQ;

public static class RabbitMqServiceExtensions
{
    /// <summary>
    /// RabbitMQ + MassTransit + IEventBus registrasyonunu yapar.
    /// Consumer assembly'leri parametre olarak geçirilebilir.
    /// </summary>
    public static IServiceCollection AddRabbitMqEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        params Type[] consumerTypes)
    {
        services.AddMassTransit(busConfig =>
        {
            // ─────────────────────────────────────────────────────────────
            // Consumer'ları MassTransit'e tanıtma
            // Her consumer tipi bir queue oluşturur ve ilgili event'leri dinler
            // ─────────────────────────────────────────────────────────────
            foreach (var consumerType in consumerTypes)
            {
                busConfig.AddConsumer(consumerType);
            }

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                // ─────────────────────────────────────────────────────────────
                // Aspire Connection String Desteği
                // ─────────────────────────────────────────────────────────────
                // 📚 EĞİTİCİ NOT (Tech-Tutor):
                //
                // Aspire ile çalışırken (AppHost üzerinden başlatıldığında):
                // → AppHost'taki builder.AddRabbitMQ("rabbitmq") çağrısı,
                //   her servise şu env var'ı enjekte eder:
                //   ConnectionStrings__rabbitmq = "amqp://user:pass@hostname:5672"
                //
                // Standalone çalışırken (docker-compose veya lokal):
                // → ConnectionStrings:rabbitmq yoktur.
                //   Eski RabbitMQ:Host / Username / Password ayarlarına dönülür.
                //
                // Bu "dual-mode" yaklaşım sayesinde:
                // → Geliştirici "dotnet run" ile AppHost'u başlatır → Aspire yönetir
                // → CI/CD pipeline'ında docker-compose.prod.yml kullanılır → standalone
                // Her iki senaryoda da servis sorunsuz çalışır.
                //
                // MassTransit Uri Format:
                // amqp://[kullanıcı:şifre@]host[:port][/sanal-host]
                // Aspire bu formatı doğrudan üretir, MassTransit kabul eder.
                // ─────────────────────────────────────────────────────────────
                var rabbitConnectionString = configuration.GetConnectionString("rabbitmq");

                if (!string.IsNullOrEmpty(rabbitConnectionString))
                {
                    // Aspire yönetimli ortam: URI formatında connection string
                    cfg.Host(new Uri(rabbitConnectionString));
                }
                else
                {
                    // Standalone ortam: appsettings.json'daki ayrı ayrı değerler
                    cfg.Host(
                        configuration["RabbitMQ:Host"] ?? "localhost",
                        configuration["RabbitMQ:VirtualHost"] ?? "/",
                        h =>
                        {
                            h.Username(configuration["RabbitMQ:Username"] ?? "lms_user");
                            h.Password(configuration["RabbitMQ:Password"] ?? "lms_password_2024");
                        });
                }

                // ─────────────────────────────────────────────────────
                // Retry Policy: Hatalı mesajlar için yeniden deneme
                // 3 kez dener, her denemede 5 saniye bekler.
                // Tüm denemeler başarısız olursa → Dead Letter Queue'ya gider.
                // ─────────────────────────────────────────────────────
                cfg.UseMessageRetry(retryConfig =>
                {
                    retryConfig.Interval(3, TimeSpan.FromSeconds(5));
                });

                // Consumer endpoint'lerini otomatik konfigüre et
                cfg.ConfigureEndpoints(context);
            });
        });

        // IEventBus → MassTransitEventBus olarak DI container'a kaydet
        services.AddScoped<IEventBus, MassTransitEventBus>();

        return services;
    }
}
