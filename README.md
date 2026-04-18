# 📚 Kütüphane Yönetim Sistemi (LMS) - Microservices Architecture

Bu proje, **modern mikroservis mimarisi** prensipleriyle geliştirilmiş, ölçeklenebilir bir Kütüphane Yönetim Sistemi'dir. **.NET 8** ekosistemi üzerine inşa edilen bu eğitim projesi, endüstri standartlarında kullanılan teknolojileri ve tasarım kalıplarını (CQRS, DDD, Event-Driven Architecture) içermektedir.

---

## 🎯 Projenin Amacı ve Özeti

Bu projenin temel amacı, dağıtık sistemler dünyasında sıkça karşılaşılan problemleri ele almak ve en iyi uygulama yöntemlerini (best practices) sergilemek:
- **Yüksek Performans:** Okuma ve yazma işlemlerini ayırarak (CQRS) veritabanı yükünü dengelemek.
- **Ölçeklenebilirlik:** Her servisin bağımsız olarak ölçeklenebilmesini sağlamak.
- **Dayanıklılık (Resiliency):** Servisler arası asenkron iletişim (RabbitMQ) ile sistemin hataya dayanıklı olmasını sağlamak.
- **İzlenebilirlik:** Dağıtık loglama ve tracing (Elasticsearch, Kibana, OpenTelemetry) ile sistem sağlığını takip etmek.

---

## 🏗️ Mimari ve Tasarım Desenleri

Proje, **Clean Architecture** prensiplerine sadık kalınarak tasarlanmıştır.

### Öne Çıkan Mimari Desenler:
1.  **Microservices Architecture:** Uygulama, sorumluluklarına göre (Bounded Contexts) küçük, bağımsız servislere ayrılmıştır.
2.  **CQRS (Command Query Responsibility Segregation):** 
    -   **Write Side (Commands):** Veri tutarlılığı (consistency) ön plandadır. PostgreSQL kullanılır.
    -   **Read Side (Queries):** Performans ön plandadır. MongoDB veya Elasticsearch kullanılır.
3.  **Domain-Driven Design (DDD):** İş kuralları, zengin domain modelleri (Aggregate Roots, Entities, Value Objects) içinde kapsüllenmiştir.
4.  **Event-Driven Architecture:** Servisler birbirleriyle doğrudan (senkron) haberleşmek yerine, **RabbitMQ** üzerinden olaylar (events) fırlatarak (asenkron) haberleşir. 
    -   *Örnek:* Bir kitap ödünç alındığında (`BookBorrowedEvent`), bildirim servisi bunu dinler ve kullanıcıya e-posta gönderir.
5.  **Outbox Pattern:** Event'lerin kaybolmaması ve veritabanı işlemlerinin transactional bütünlüğü için kullanılmıştır.

---

## 🛠️ Teknoloji Yığını (Tech Stack)

Projede kullanılan temel teknolojiler ve kullanım alanları:

| Teknoloji | Kullanım Alanı | Açıklama |
| :--- | :--- | :--- |
| **.NET 8** | Backend Framework | Tüm mikroservisler için ana geliştirme platformu. |
| **PostgreSQL** | Veritabanı (Write) | İlişkisel veriler, kullanıcı bilgileri ve kitap kayıtları için ana veri kaynağı. |
| **MongoDB** | Veritabanı (Read) | CQRS Read modelleri için optimize edilmiş NoSQL veritabanı. |
| **Redis** | Distributed Cache | Sık erişilen verilerin (örn. katalog listeleri) önbelleğe alınması için. |
| **RabbitMQ** | Message Broker | Servisler arası asenkron iletişim ve olay yönetimi. |
| **Elasticsearch** | Logging & Search | Merkezi loglama (Serilog) ve gelişmiş metin arama yetenekleri. |
| **Kibana** | Monitoring | Elasticsearch üzerindeki logları görselleştirmek için dashboard. |
| **Docker & Docker Compose** | Containerization | Tüm altyapıyı ve servisleri izole ortamda, tek komutla ayağa kaldırmak için. |
| **OpenTelemetry** | Tracing | Dağıtık sistemde bir isteğin (request) izini sürmek için. |
| **MediatR** | In-Process Messaging | Servis içi Command/Query ayrımını yönetmek için. |

---

## 🧩 Servisler ve Modüller

Proje aşağıdaki ana servislerden oluşmaktadır:

### 1. Catalog Service
Kitapların yönetildiği servistir.
- Kitap ekleme, güncelleme, silme (Admin).
- Kitap arama, listeleme ve detay görüntüleme.
- **Teknolojiler:** PostgreSQL, MongoDB, Elasticsearch.

### 2. Borrowing Service (Ödünç Alma)
Ödünç alma süreçlerini yönetir.
- Bir kullanıcının kitap ödünç alması.
- İade işlemleri.
- Ceza/gecikme hesaplamaları (eğer varsa).
- **İletişim:** `BookBorrowed` eventi fırlatır.

### 3. Identity Service
Kimlik doğrulama ve yetkilendirme servisidir.
- Kullanıcı kaydı ve girişi.
- JWT (JSON Web Token) üretimi ve doğrulaması.
- Rol yönetimi (Admin, User).

### 4. Notification Service
Kullanıcıları bilgilendiren servistir.
- Arka planda çalışır (Background Worker).
- RabbitMQ kuyruklarını dinler (örn. `BookBorrowedEvent`, `UserCreatedEvent`).
- E-posta veya SMS simülasyonu yapar.

### 🏗️ Building Blocks (Core)
Tüm servislerin ortak kullandığı kütüphaneler:
- **SharedKernel:** Ortak Entity, ValueObject, Exception sınıfları.
- **EventBus:** RabbitMQ bağlantı ve mesajlaşma soyutlamaları.
- **Caching:** Redis bağlantı ve cache yönetimi soyutlamaları.

---

## 🚀 Kurulum ve Çalıştırma

Projeyi yerel ortamınızda çalıştırmak için aşağıdaki adımları izleyin.

### Gereksinimler
- **Docker Desktop** (Yüklü ve çalışır durumda olmalı)
- **.NET 8 SDK** (Geliştirme yapacaksanız)

### Adım 1: Projeyi Klonlayın
```bash
git clone https://github.com/kullaniciadi/proje-adi.git
cd proje-adi
```

### Adım 2: Altyapıyı Ayağa Kaldırın
Tüm veritabanları ve mesaj kuyruklarını Docker ile başlatın:
```bash
docker-compose up -d
```
*Not: İlk çalıştırmada imajların indirilmesi biraz zaman alabilir.*

### Adım 3: Servisleri Başlatın
Tercihinize göre servisleri IDE üzerinden (Visual Studio / Rider) veya terminalden başlatabilirsiniz.

#### Terminal ile (Root dizinde):
```bash
dotnet run --project src/Services/Catalog/Catalog.API/Catalog.API.csproj
dotnet run --project src/Services/Identity/Identity.API/Identity.API.csproj
# Diğer servisler için de benzer şekilde...
```

### Adım 4: Erişim
Servisler ayağa kalktıktan sonra aşağıdaki adreslerden erişebilirsiniz (Portlar `docker-compose.yml` veya `launchSettings.json` dosyasına göre değişebilir):

- **Catalog API (Swagger):** `http://localhost:5000/swagger`
- **RabbitMQ Management:** `http://localhost:15672` (Kullanıcı adı/Şifre: `guest`/`guest` veya `lms_user`/`lms_password_2024`)
- **Kibana (Logs):** `http://localhost:5601`

---