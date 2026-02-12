// =============================================================================
// Notification Consumers — Event Handlers
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// MassTransit Consumer Yapısı:
// → IConsumer<TEvent> interface'ini implement eder.
// → MassTransit, mesaj geldiğinde Consume() metodunu çağırır.
// → Her consumer kendi queue'sunu dinler (isolation).
//
// Consumer vs Handler:
// → MediatR INotificationHandler: Aynı process içi (in-process)
// → MassTransit IConsumer: Farklı process/servis arası (out-of-process)
//   MassTransit consumer'ları farklı makinelerde çalışabilir!
//
// Competing Consumer Pattern:
// → Aynı consumer'dan birden fazla instance çalıştırılabilir.
//   RabbitMQ, mesajları instance'lar arasında dağıtır (load balancing).
//   Bu sayede yüksek bildirim yükü paralel işlenebilir.
// =============================================================================

using EventBus.Abstractions.Events;
using MassTransit;

namespace Notification.API.Consumers;

// ─────────────────────────────────────────────────────────────────────────────
// UserRegistered Consumer — Hoşgeldin bildirimi
// ─────────────────────────────────────────────────────────────────────────────
public class UserRegisteredConsumer : IConsumer<UserRegisteredIntegrationEvent>
{
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(ILogger<UserRegisteredConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        var msg = context.Message;

        // ─────────────────────────────────────────────────────
        // GERÇEK UYGULAMADA:
        // → MailKit ile SMTP üzerinden e-posta gönderilir
        // → SendGrid, AWS SES gibi servisler kullanılabilir
        // → Template engine ile güzel HTML e-postalar oluşturulur
        //
        // ŞU AN: Loglama ile simüle ediyoruz
        // ─────────────────────────────────────────────────────
        _logger.LogInformation(
            "📧 [Bildirim] Hoşgeldin e-postası gönderildi → {Email} ({FullName})",
            msg.Email, msg.FullName);

        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BookCreated Consumer — Yeni kitap bildirimi 
// ─────────────────────────────────────────────────────────────────────────────
public class BookCreatedConsumer : IConsumer<BookCreatedNotification>
{
    private readonly ILogger<BookCreatedConsumer> _logger;

    public BookCreatedConsumer(ILogger<BookCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BookCreatedNotification> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "📚 [Bildirim] Yeni kitap eklendi → '{Title}' ({Category})",
            msg.Title, msg.Category);

        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BookBorrowed Consumer — Ödünç alma onay bildirimi
// ─────────────────────────────────────────────────────────────────────────────
public class BookBorrowedConsumer : IConsumer<BookBorrowedNotification>
{
    private readonly ILogger<BookBorrowedConsumer> _logger;

    public BookBorrowedConsumer(ILogger<BookBorrowedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BookBorrowedNotification> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "📖 [Bildirim] Kitap ödünç alındı → '{BookTitle}' (Kullanıcı: {UserId})",
            msg.BookTitle, msg.UserId);

        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BookReturned Consumer — İade onay bildirimi
// ─────────────────────────────────────────────────────────────────────────────
public class BookReturnedConsumer : IConsumer<BookReturnedNotification>
{
    private readonly ILogger<BookReturnedConsumer> _logger;

    public BookReturnedConsumer(ILogger<BookReturnedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BookReturnedNotification> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "✅ [Bildirim] Kitap iade edildi → BookId: {BookId} (Kullanıcı: {UserId})",
            msg.BookId, msg.UserId);

        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Integration Event Kontrattan (Shared event types consumed by this service)
// Gerçek üretim ortamında bunlar ortak NuGet paketi ile paylaşılır.
// ─────────────────────────────────────────────────────────────────────────────
public record UserRegisteredIntegrationEvent(
    string UserId, string FullName, string Email) : IntegrationEvent;

public record BookCreatedNotification(
    Guid BookId, string Title, string Category) : IntegrationEvent;

public record BookBorrowedNotification(
    Guid BorrowingId, Guid BookId, string UserId, string BookTitle) : IntegrationEvent;

public record BookReturnedNotification(
    Guid BorrowingId, Guid BookId, string UserId) : IntegrationEvent;
