// =============================================================================
// Projects.cs — Servis Proje Metadata Tanımları
// =============================================================================
// 📚 EĞİTİCİ NOT (Tech-Tutor):
//
// Aspire AppHost, AddProject<T>() metodunda T olarak "proje metadata" sınıfı
// kullanır. Bu sınıflar IProjectMetadata arayüzünü implement eder.
//
// NEDEN Manuel Tanım?
// → Eski yaklaşımda (workload tabanlı) bu sınıflar SDK/workload tarafından
//   otomatik üretilirdi (Projects.Catalog_API gibi tip adları).
//   Yeni yaklaşımda (sadece NuGet paketi) bu sınıfları kendiniz tanımlarsınız.
//
// IProjectMetadata Arayüzü:
// → Tek gereksinimi: string ProjectPath { get; }
// → Bu path, AppHost.csproj'un bulunduğu dizine göre görecelidir.
//   Veya mutlak yol da kullanılabilir.
//
// Tip-Güvenlik Avantajı:
// → AddProject<CatalogApiProject>("catalog-api")
//   Yanlış proje yolu → derleme hatası (runtime'da değil)
//   vs. string tabanlı:
//   AddProject("catalog-api", "../yanlis/yol/proje.csproj")
//   Yanlış yol → runtime hatası
//
// AppHost'ta Kullanımı:
//   builder.AddProject<CatalogApiProject>("catalog-api")
//   builder.AddProject<IdentityApiProject>("identity-api")
//   ...
// =============================================================================

using Aspire.Hosting;

// Catalog API (Clean Architecture — 4 Katman)
// ProjectPath, AppHost.csproj'un bulunduğu src/AppHost/ dizinine görecelidir
sealed class CatalogApiProject : IProjectMetadata
{
    public string ProjectPath => "../Services/Catalog/Catalog.API/Catalog.API.csproj";
}

// Identity API — JWT Authentication & User Management
sealed class IdentityApiProject : IProjectMetadata
{
    public string ProjectPath => "../Services/Identity/Identity.API/Identity.API.csproj";
}

// Borrowing API — Kitap Ödünç Alma / İade Yönetimi
sealed class BorrowingApiProject : IProjectMetadata
{
    public string ProjectPath => "../Services/Borrowing/Borrowing.API/Borrowing.API.csproj";
}

// Notification API — Event Consumer, Bildirim Gönderici
sealed class NotificationApiProject : IProjectMetadata
{
    public string ProjectPath => "../Services/Notification/Notification.API/Notification.API.csproj";
}

// API Gateway — YARP Reverse Proxy (solution root'taki csproj)
sealed class ApiGatewayProject : IProjectMetadata
{
    // ../../ → src/AppHost/ → src/ → solution root
    public string ProjectPath => "../../Training-dotnet.csproj";
}
