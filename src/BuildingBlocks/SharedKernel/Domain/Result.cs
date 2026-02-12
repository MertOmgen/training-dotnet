// =============================================================================
// Result<T> Pattern — İşlem Sonucu Sarmalayıcı
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// NEDEN Result Pattern?
// → Exception fırlatmak yerine, başarılı/başarısız sonuçları açıkça
//   modelleyen bir yaklaşımdır.
// → Exception'lar "exceptional" (beklenmedik) durumlar içindir.
//   İş kuralı ihlalleri ("Kitap bulunamadı" gibi) exception değildir.
//
// NASIL Çalışır?
// → Result.Success(value) — başarılı sonuç döner
// → Result.Failure("hata mesajı") — hata ile sonuç döner
// → Handler'larda if (result.IsFailure) kontrolü yapılır
//
// ALTERNATİF:
// → FluentResults kütüphanesi: Daha zengin özellikler sunar ama
//   öğrenme amaçlı kendi implementasyonumuzu yazıyoruz.
// → OneOf<T0, T1>: Discriminated unions yaklaşımı (C# 9+ ile popüler).
// =============================================================================

namespace SharedKernel.Domain;

/// <summary>
/// İşlem sonucunu temsil eden generic sarmalayıcı.
/// Exception fırlatmak yerine başarı/hata durumunu açıkça modelleriz.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(string error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    /// <summary>Başarılı sonuç oluşturur</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Hatalı sonuç oluşturur</summary>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// Implicit conversion: T → Result&lt;T&gt; (başarılı)
    /// Kullanım: return myBook; // otomatik olarak Result.Success(myBook) olur
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
}

/// <summary>
/// Değer döndürmeyen işlemler için Result (non-generic versiyon)
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    private Result(bool isSuccess, string? error = null)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true);
    public static Result Failure(string error) => new(false, error);
}
