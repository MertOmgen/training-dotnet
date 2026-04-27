# CI/CD + Unit Test Entegrasyonu — Uygulama Planı

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GitHub Actions CI pipeline (build + test) kurmak ve xUnit + Moq + FluentAssertions ile Catalog servisine derin unit testler, diğer servislere smoke testler eklemek.

**Architecture:** Servis başına bir test projesi (`tests/` klasöründe), Catalog'da Domain/Application/API alt klasörleri. Smoke testler `WebApplicationFactory` + `ConfigureTestServices` ile Aspire bağımlılıklarından izole çalışır. GitHub Actions `push`/`PR` tetiklemesiyle otomatik `build + test` yapar.

**Tech Stack:** xUnit 2.x, Moq 4.x, FluentAssertions 6.x, Microsoft.AspNetCore.Mvc.Testing, coverlet, GitHub Actions ubuntu-latest, .NET 8

---

## Chunk 1: Test Proje İskeletleri + Solution Entegrasyonu

### Dosyalar

- Create: `tests/Catalog.Tests/Catalog.Tests.csproj`
- Create: `tests/Borrowing.Tests/Borrowing.Tests.csproj`
- Create: `tests/Identity.Tests/Identity.Tests.csproj`
- Create: `tests/Notification.Tests/Notification.Tests.csproj`
- Modify: `Training-dotnet.slnx`

---

### Task 1: Catalog.Tests projesi oluştur

**Files:**
- Create: `tests/Catalog.Tests/Catalog.Tests.csproj`

- [ ] **Step 1: `tests/Catalog.Tests/Catalog.Tests.csproj` dosyasını oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <RootNamespace>Catalog.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="FluentValidation.TestHelper" Version="11.11.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Services\Catalog\Catalog.Domain\Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\src\Services\Catalog\Catalog.Application\Catalog.Application.csproj" />
    <ProjectReference Include="..\..\src\Services\Catalog\Catalog.API\Catalog.API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Projeyi derle — restore + build**

```powershell
cd "d:\_Merdo-Developing\_Merdo-Training\.NET8\Training-dotnet"
dotnet restore tests/Catalog.Tests/Catalog.Tests.csproj
dotnet build tests/Catalog.Tests/Catalog.Tests.csproj --no-restore
```

Beklenen: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add tests/Catalog.Tests/Catalog.Tests.csproj
git commit -m "test: Catalog.Tests projesi oluşturuldu"
```

---

### Task 2: Borrowing, Identity, Notification test projeleri oluştur

**Files:**
- Create: `tests/Borrowing.Tests/Borrowing.Tests.csproj`
- Create: `tests/Identity.Tests/Identity.Tests.csproj`
- Create: `tests/Notification.Tests/Notification.Tests.csproj`

- [ ] **Step 1: `tests/Borrowing.Tests/Borrowing.Tests.csproj` oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <RootNamespace>Borrowing.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Services\Borrowing\Borrowing.API\Borrowing.API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: `tests/Identity.Tests/Identity.Tests.csproj` oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <RootNamespace>Identity.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Services\Identity\Identity.API\Identity.API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: `tests/Notification.Tests/Notification.Tests.csproj` oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <RootNamespace>Notification.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Services\Notification\Notification.API\Notification.API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Tüm projeleri derle**

```powershell
dotnet restore tests/Borrowing.Tests/Borrowing.Tests.csproj
dotnet restore tests/Identity.Tests/Identity.Tests.csproj
dotnet restore tests/Notification.Tests/Notification.Tests.csproj
dotnet build tests/Borrowing.Tests/Borrowing.Tests.csproj --no-restore
dotnet build tests/Identity.Tests/Identity.Tests.csproj --no-restore
dotnet build tests/Notification.Tests/Notification.Tests.csproj --no-restore
```

Beklenen: Her biri `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git add tests/
git commit -m "test: Borrowing, Identity, Notification test projeleri oluşturuldu"
```

---

### Task 3: Test projelerini solution'a ekle

**Files:**
- Modify: `Training-dotnet.slnx`

- [ ] **Step 1: `Training-dotnet.slnx` dosyasına `</Solution>` kapanış etiketinden önce şu bloğu ekle**

```xml
  <!-- ═══════════════════════════════════════════════════════════════════ -->
  <!-- Tests -->
  <!-- ═══════════════════════════════════════════════════════════════════ -->
  <Folder Name="/Tests/">
    <Project Path="tests/Catalog.Tests/Catalog.Tests.csproj" />
    <Project Path="tests/Borrowing.Tests/Borrowing.Tests.csproj" />
    <Project Path="tests/Identity.Tests/Identity.Tests.csproj" />
    <Project Path="tests/Notification.Tests/Notification.Tests.csproj" />
  </Folder>
```

- [ ] **Step 2: Solution build kontrolü**

```powershell
dotnet build Training-dotnet.slnx
```

Beklenen: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add Training-dotnet.slnx
git commit -m "chore: test projeleri solution'a eklendi"
```

---

## Chunk 2: Catalog Domain Unit Testleri

### Dosyalar

- Create: `tests/Catalog.Tests/Domain/BookTests.cs`

---

### Task 4: Book domain entity unit testleri

**Files:**
- Create: `tests/Catalog.Tests/Domain/BookTests.cs`

- [ ] **Step 1: Test dosyasını oluştur — başarılı oluşturma testi**

```csharp
using Catalog.Domain.Entities;
using Catalog.Domain.Events;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using SharedKernel.Domain;

namespace Catalog.Tests.Domain;

public class BookTests
{
    // ─────────────────────────────────────────────────────────────
    // Yardımcı: Geçerli bir Author oluştur
    // ─────────────────────────────────────────────────────────────
    private static Author ValidAuthor()
    {
        var result = Author.Create("Orhan", "Pamuk");
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Create_WithValidParameters_ReturnsSuccessWithCorrectProperties()
    {
        // Arrange
        var author = ValidAuthor();

        // Act
        var result = Book.Create(
            title: "Kar",
            isbn: "9789750719387",
            description: "Roman",
            author: author,
            publishedYear: 2002,
            category: "Roman",
            totalCopies: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Kar");
        result.Value.Isbn.Should().Be("9789750719387");
        result.Value.TotalCopies.Should().Be(5);
        result.Value.AvailableCopies.Should().Be(5);
        result.Value.IsActive.Should().BeTrue();
        result.Value.Author.FullName.Should().Be("Orhan Pamuk");
    }
}
```

- [ ] **Step 2: Testi çalıştır — PASS bekleniyor**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "Category=Unit" -v normal
```

Beklenen: `1 passed, 0 failed`

- [ ] **Step 3: Hata senaryosu testlerini ekle — boş title**

`BookTests.cs` içine `BookTests` sınıfına şu testleri ekle:

```csharp
    [Trait("Category", "Unit")]
    [Fact]
    public void Create_WithEmptyTitle_ReturnsFailure()
    {
        var author = ValidAuthor();
        var result = Book.Create("", "9789750719387", null, author, 2002, "Roman", 5);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Create_WithNegativeTotalCopies_ReturnsFailure()
    {
        var author = ValidAuthor();
        var result = Book.Create("Kar", "9789750719387", null, author, 2002, "Roman", -1);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("negatif");
    }

    [Trait("Category", "Unit")]
    [Theory]
    [InlineData(999)]
    [InlineData(3000)]
    public void Create_WithInvalidPublishedYear_ReturnsFailure(int invalidYear)
    {
        var author = ValidAuthor();
        var result = Book.Create("Kar", "9789750719387", null, author, invalidYear, "Roman", 5);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("yıl");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Create_Successfully_RaisesBookCreatedDomainEvent()
    {
        var author = ValidAuthor();
        var result = Book.Create("Kar", "9789750719387", null, author, 2002, "Roman", 5);

        result.IsSuccess.Should().BeTrue();

        // AggregateRoot.DomainEvents koleksiyonunu kontrol et
        // SharedKernel.Domain.Entity<T> tabanlı entity'lerde GetDomainEvents() veya DomainEvents property'si beklenir
        var book = result.Value!;
        var events = ((dynamic)book).DomainEvents as IEnumerable<object>;
        events.Should().ContainSingle(e => e is BookCreatedDomainEvent);
    }
```

> **NOT:** `DomainEvents` property'si `Entity<T>` veya `AggregateRoot<T>` base sınıfında tanımlı olmalıdır. Eğer property adı farklıysa (örn. `_domainEvents` private list), bu testin erişim yöntemi buna göre güncellenmeli. `Entity.cs` dosyasına bakarak doğrula.

- [ ] **Step 4: Tüm domain testlerini çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "Category=Unit" -v normal
```

Beklenen: `5 passed, 0 failed`

- [ ] **Step 5: Commit**

```powershell
git add tests/Catalog.Tests/Domain/BookTests.cs
git commit -m "test: Book domain entity unit testleri eklendi"
```

---

## Chunk 3: Catalog Application Unit Testleri

### Dosyalar

- Create: `tests/Catalog.Tests/Application/CreateBookCommandHandlerTests.cs`
- Create: `tests/Catalog.Tests/Application/CreateBookCommandValidatorTests.cs`
- Create: `tests/Catalog.Tests/Application/GetBookByIdQueryHandlerTests.cs`

---

### Task 5: CreateBookCommandHandler testleri

**Files:**
- Create: `tests/Catalog.Tests/Application/CreateBookCommandHandlerTests.cs`

- [ ] **Step 1: ISBN çakışma testi oluştur**

```csharp
using Catalog.Application.Commands;
using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application;

public class CreateBookCommandHandlerTests
{
    private readonly Mock<IBookRepository> _repoMock;
    private readonly CreateBookCommandHandler _handler;

    public CreateBookCommandHandlerTests()
    {
        _repoMock = new Mock<IBookRepository>();
        _handler = new CreateBookCommandHandler(_repoMock.Object);
    }

    private static CreateBookCommand ValidCommand(string isbn = "9789750719387") => new(
        Title: "Kar",
        Isbn: isbn,
        Description: null,
        AuthorFirstName: "Orhan",
        AuthorLastName: "Pamuk",
        PublishedYear: 2002,
        Category: "Roman",
        TotalCopies: 5);

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Handle_WhenIsbnAlreadyExists_ReturnsFailure()
    {
        // Arrange — repo mevcut kitap döndürür
        var authorResult = Author.Create("Orhan", "Pamuk");
        var existingBook = Book.Create("Mevcut", "9789750719387", null,
            authorResult.Value!, 2000, "Roman", 1).Value!;

        _repoMock
            .Setup(r => r.GetByIsbnAsync("9789750719387", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBook);

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("9789750719387");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Testi çalıştır — PASS bekleniyor**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "FullyQualifiedName~CreateBookCommandHandlerTests" -v normal
```

Beklenen: `1 passed, 0 failed`

- [ ] **Step 3: Başarılı oluşturma testini ekle**

`CreateBookCommandHandlerTests` sınıfına ekle:

```csharp
    [Trait("Category", "Unit")]
    [Fact]
    public async Task Handle_WhenIsbnIsUnique_AddsBookAndReturnsSuccess()
    {
        // Arrange — repo null döndürür (ISBN yok)
        _repoMock
            .Setup(r => r.GetByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Book?)null);

        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Kar");
        result.Value.Isbn.Should().Be("9789750719387");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 4: Testleri çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "FullyQualifiedName~CreateBookCommandHandlerTests" -v normal
```

Beklenen: `2 passed, 0 failed`

- [ ] **Step 5: Commit**

```powershell
git add tests/Catalog.Tests/Application/CreateBookCommandHandlerTests.cs
git commit -m "test: CreateBookCommandHandler unit testleri eklendi"
```

---

### Task 6: CreateBookCommandValidator testleri

**Files:**
- Create: `tests/Catalog.Tests/Application/CreateBookCommandValidatorTests.cs`

- [ ] **Step 1: Validator test dosyasını oluştur**

```csharp
using Catalog.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Catalog.Tests.Application;

public class CreateBookCommandValidatorTests
{
    private readonly CreateBookCommandValidator _validator = new();

    private static CreateBookCommand ValidCommand() => new(
        Title: "Kar",
        Isbn: "9789750719387",
        Description: null,
        AuthorFirstName: "Orhan",
        AuthorLastName: "Pamuk",
        PublishedYear: 2002,
        Category: "Roman",
        TotalCopies: 5);

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Validate_WithValidCommand_HasNoErrors()
    {
        var result = await _validator.TestValidateAsync(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Validate_WithEmptyTitle_HasValidationError()
    {
        var cmd = ValidCommand() with { Title = "" };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Trait("Category", "Unit")]
    [Theory]
    [InlineData("123")]           // 3 hane — geçersiz
    [InlineData("12345678901234")] // 14 hane — geçersiz
    [InlineData("abcdefghij")]    // harf — geçersiz
    public async Task Validate_WithInvalidIsbn_HasValidationError(string invalidIsbn)
    {
        var cmd = ValidCommand() with { Isbn = invalidIsbn };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Isbn);
    }

    [Trait("Category", "Unit")]
    [Theory]
    [InlineData("1234567890")]    // 10 hane — geçerli
    [InlineData("9789750719387")] // 13 hane — geçerli
    public async Task Validate_WithValidIsbn_HasNoIsbnError(string validIsbn)
    {
        var cmd = ValidCommand() with { Isbn = validIsbn };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Isbn);
    }
}
```

- [ ] **Step 2: Testleri çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "FullyQualifiedName~CreateBookCommandValidatorTests" -v normal
```

Beklenen: `5 passed, 0 failed`

- [ ] **Step 3: Commit**

```powershell
git add tests/Catalog.Tests/Application/CreateBookCommandValidatorTests.cs
git commit -m "test: CreateBookCommandValidator unit testleri eklendi"
```

---

### Task 7: GetBookByIdQueryHandler testleri

**Files:**
- Create: `tests/Catalog.Tests/Application/GetBookByIdQueryHandlerTests.cs`

- [ ] **Step 1: Test dosyasını oluştur**

```csharp
using Catalog.Application.DTOs;
using Catalog.Application.Queries;
using FluentAssertions;
using MongoDB.Driver;
using Moq;

namespace Catalog.Tests.Application;

public class GetBookByIdQueryHandlerTests
{
    private readonly Mock<IMongoDatabase> _mongoDatabaseMock;
    private readonly Mock<IMongoCollection<BookDto>> _collectionMock;
    private readonly GetBookByIdQueryHandler _handler;

    public GetBookByIdQueryHandlerTests()
    {
        _collectionMock = new Mock<IMongoCollection<BookDto>>();
        _mongoDatabaseMock = new Mock<IMongoDatabase>();
        _mongoDatabaseMock
            .Setup(db => db.GetCollection<BookDto>("books", null))
            .Returns(_collectionMock.Object);

        _handler = new GetBookByIdQueryHandler(_mongoDatabaseMock.Object);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Handle_WhenBookNotFound_ReturnsNull()
    {
        // Arrange — cursor boş döndürür
        var bookId = Guid.NewGuid();
        var cursorMock = new Mock<IAsyncCursor<BookDto>>();
        cursorMock.Setup(c => c.Current).Returns(Enumerable.Empty<BookDto>());
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BookDto>>(),
                It.IsAny<FindOptions<BookDto, BookDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorMock.Object);

        var query = new GetBookByIdQuery(bookId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Handle_WhenBookFound_ReturnsBookDto()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var expectedDto = new BookDto(
            Id: bookId,
            Title: "Kar",
            Isbn: "9789750719387",
            Description: null,
            AuthorFirstName: "Orhan",
            AuthorLastName: "Pamuk",
            AuthorFullName: "Orhan Pamuk",
            PublishedYear: 2002,
            Category: "Roman",
            TotalCopies: 5,
            AvailableCopies: 5,
            IsActive: true,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null);

        var cursorMock = new Mock<IAsyncCursor<BookDto>>();
        cursorMock.Setup(c => c.Current).Returns(new[] { expectedDto });
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BookDto>>(),
                It.IsAny<FindOptions<BookDto, BookDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorMock.Object);

        var query = new GetBookByIdQuery(bookId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(bookId);
        result.Title.Should().Be("Kar");
    }
}
```

- [ ] **Step 2: Testleri çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "FullyQualifiedName~GetBookByIdQueryHandlerTests" -v normal
```

Beklenen: `2 passed, 0 failed`

- [ ] **Step 3: Tüm Catalog unit testlerini çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "Category=Unit" -v normal
```

Beklenen: Tüm testler pass.

- [ ] **Step 4: Commit**

```powershell
git add tests/Catalog.Tests/Application/GetBookByIdQueryHandlerTests.cs
git commit -m "test: GetBookByIdQueryHandler unit testleri eklendi"
```

---

## Chunk 4: Smoke Testler (appsettings.Test.json + WebApplicationFactory)

### Dosyalar

- Create: `src/Services/Catalog/Catalog.API/appsettings.Test.json`
- Create: `src/Services/Borrowing/Borrowing.API/appsettings.Test.json`
- Create: `src/Services/Identity/Identity.API/appsettings.Test.json`
- Create: `src/Services/Notification/Notification.API/appsettings.Test.json`
- Create: `tests/Catalog.Tests/API/CatalogApiSmokeTests.cs`
- Create: `tests/Borrowing.Tests/BorrowingApiSmokeTests.cs`
- Create: `tests/Identity.Tests/IdentityApiSmokeTests.cs`
- Create: `tests/Notification.Tests/NotificationApiSmokeTests.cs`

---

### Task 8: appsettings.Test.json dosyalarını oluştur

**Files:**
- Create: Her API projesine `appsettings.Test.json`

- [ ] **Step 1: `src/Services/Catalog/Catalog.API/appsettings.Test.json` oluştur**

```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Port=0;Database=test;Username=test;Password=test",
    "mongodb": "mongodb://localhost:0",
    "redis": "localhost:0"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

- [ ] **Step 2: `src/Services/Borrowing/Borrowing.API/appsettings.Test.json` oluştur**

```json
{
  "ConnectionStrings": {
    "borrowing-db": "Host=localhost;Port=0;Database=test;Username=test;Password=test"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

- [ ] **Step 3: `src/Services/Identity/Identity.API/appsettings.Test.json` oluştur**

```json
{
  "ConnectionStrings": {
    "identity-db": "Host=localhost;Port=0;Database=test;Username=test;Password=test"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

- [ ] **Step 4: `src/Services/Notification/Notification.API/appsettings.Test.json` oluştur**

```json
{
  "ConnectionStrings": {
    "notification-db": "Host=localhost;Port=0;Database=test;Username=test;Password=test"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

- [ ] **Step 5: appsettings.Test.json dosyaları her API projesinin .csproj'unda CopyToOutputDirectory ile dahil edilmeli.**

Her API `csproj` dosyasına şu `ItemGroup`'u ekle (eğer yoksa):

```xml
<ItemGroup>
  <Content Update="appsettings.Test.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Bu adım **4 csproj dosyasında** yapılır:
- `src/Services/Catalog/Catalog.API/Catalog.API.csproj`
- `src/Services/Borrowing/Borrowing.API/Borrowing.API.csproj`
- `src/Services/Identity/Identity.API/Identity.API.csproj`
- `src/Services/Notification/Notification.API/Notification.API.csproj`

- [ ] **Step 6: Commit**

```powershell
git add src/Services/Catalog/Catalog.API/appsettings.Test.json
git add src/Services/Borrowing/Borrowing.API/appsettings.Test.json
git add src/Services/Identity/Identity.API/appsettings.Test.json
git add src/Services/Notification/Notification.API/appsettings.Test.json
git add src/Services/Catalog/Catalog.API/Catalog.API.csproj
git add src/Services/Borrowing/Borrowing.API/Borrowing.API.csproj
git add src/Services/Identity/Identity.API/Identity.API.csproj
git add src/Services/Notification/Notification.API/Notification.API.csproj
git commit -m "test: appsettings.Test.json dosyaları eklendi"
```

---

### Task 9: Catalog API smoke testleri

**Files:**
- Create: `tests/Catalog.Tests/API/CatalogApiSmokeTests.cs`

- [ ] **Step 1: CustomWebApplicationFactory yardımcı sınıfını oluştur**

`tests/Catalog.Tests/API/CatalogApiSmokeTests.cs`:

```csharp
using Catalog.Domain.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;

namespace Catalog.Tests.API;

/// <summary>
/// Aspire bağımlılıklarını mock'layan WebApplicationFactory.
/// Gerçek DB/Redis/RabbitMQ bağlantısı kurulmaz.
/// </summary>
public class CatalogApiFactory : WebApplicationFactory<Catalog.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // IBookRepository → Mock (PostgreSQL yerine)
            services.RemoveAll<IBookRepository>();
            services.AddSingleton(Mock.Of<IBookRepository>());

            // IMongoDatabase → Mock (MongoDB yerine)
            services.RemoveAll<IMongoDatabase>();
            services.AddSingleton(Mock.Of<IMongoDatabase>());
        });
    }
}

public class CatalogApiSmokeTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client;

    public CatalogApiSmokeTests(CatalogApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Trait("Category", "Smoke")]
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
```

> **ZORUNLU ÖN ADIM:** `WebApplicationFactory<T>` için `Program` sınıfının test projesinden erişilebilir olması gerekir. .NET 8 top-level statements kullandığında `Program` `internal` olarak derlenir.
>
> Her API projesinin **son satırına** şunu ekle (Program.cs'in en sonuna):
>
> ```csharp
> // Test erişimi için — WebApplicationFactory<Program> kullanır
> public partial class Program { }
> ```
>
> Bu ekleme şu 4 dosyada yapılmalıdır:
> - `src/Services/Catalog/Catalog.API/Program.cs`
> - `src/Services/Borrowing/Borrowing.API/Program.cs`
> - `src/Services/Identity/Identity.API/Program.cs`
> - `src/Services/Notification/Notification.API/Program.cs`
>
> Commit: `git commit -m "chore: Program partial class eklendi (WebApplicationFactory erişimi)"`

- [ ] **Step 2: Testi çalıştır**

```powershell
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --filter "Category=Smoke" -v normal
```

Beklenen: `1 passed, 0 failed`  
Eğer startup kaynaklı hata alıyorsan, hata mesajını oku; Aspire-spesifik extension'ların CI ortamında nasıl konfigure edildiğine bak ve eksik mock'ları `ConfigureTestServices`'e ekle.

- [ ] **Step 3: Commit**

```powershell
git add tests/Catalog.Tests/API/CatalogApiSmokeTests.cs
git commit -m "test: Catalog API smoke testleri eklendi"
```

---

### Task 10: Borrowing, Identity, Notification smoke testleri

**Files:**
- Create: `tests/Borrowing.Tests/BorrowingApiSmokeTests.cs`
- Create: `tests/Identity.Tests/IdentityApiSmokeTests.cs`
- Create: `tests/Notification.Tests/NotificationApiSmokeTests.cs`

- [ ] **Step 1: `tests/Borrowing.Tests/BorrowingApiSmokeTests.cs` oluştur**

```csharp
using Borrowing.API.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Borrowing.Tests;

public class BorrowingApiFactory : WebApplicationFactory<Borrowing.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // BorrowingDbContext kaydını kaldır, InMemory ile değiştir
            services.RemoveAll<BorrowingDbContext>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.DbContextOptions<BorrowingDbContext>>();
            services.AddDbContext<BorrowingDbContext>(opts =>
                opts.UseInMemoryDatabase("BorrowingTestDb"));
        });
    }
}

public class BorrowingApiSmokeTests : IClassFixture<BorrowingApiFactory>
{
    private readonly HttpClient _client;

    public BorrowingApiSmokeTests(BorrowingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Trait("Category", "Smoke")]
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
```

> **NOT:** `UseInMemoryDatabase` için `Microsoft.EntityFrameworkCore.InMemory` paketini `Borrowing.Tests.csproj`'a ekle:
>
> ```xml
> <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
> ```

- [ ] **Step 2: `tests/Identity.Tests/Identity.Tests.csproj`'a InMemory paketini ekle**

`Identity.Tests.csproj`'daki PackageReference bloğuna ekle:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
```

- [ ] **Step 3: `tests/Identity.Tests/IdentityApiSmokeTests.cs` oluştur**

```csharp
using FluentAssertions;
using Identity.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Tests;

public class IdentityApiFactory : WebApplicationFactory<Identity.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // IdentityDbContext → InMemory (PostgreSQL yerine)
            services.RemoveAll<IdentityDbContext>();
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.AddDbContext<IdentityDbContext>(opts =>
                opts.UseInMemoryDatabase("IdentityTestDb"));
        });
    }
}

public class IdentityApiSmokeTests : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client;

    public IdentityApiSmokeTests(IdentityApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Trait("Category", "Smoke")]
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
```

> **NOT:** Identity.API'deki `DbContext` türünü `src/Services/Identity/Identity.API/` klasöründen kontrol et ve yorumu güncelle.

- [ ] **Step 3: `tests/Notification.Tests/NotificationApiSmokeTests.cs` oluştur**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Notification.Tests;

public class NotificationApiFactory : WebApplicationFactory<Notification.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // Notification servisine özel bağımlılıkları burada mock'la
        });
    }
}

public class NotificationApiSmokeTests : IClassFixture<NotificationApiFactory>
{
    private readonly HttpClient _client;

    public NotificationApiSmokeTests(NotificationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Trait("Category", "Smoke")]
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
```

- [ ] **Step 4: Tüm smoke testleri çalıştır**

```powershell
dotnet test --filter "Category=Smoke" -v normal
```

Beklenen: `4 passed, 0 failed`  
Her servise özel startup hataları için hata mesajlarını oku ve ilgili `WebApplicationFactory`'ye ek mock ekle.

- [ ] **Step 5: Commit**

```powershell
git add tests/Borrowing.Tests/BorrowingApiSmokeTests.cs
git add tests/Identity.Tests/IdentityApiSmokeTests.cs
git add tests/Notification.Tests/NotificationApiSmokeTests.cs
git commit -m "test: Borrowing, Identity, Notification smoke testleri eklendi"
```

---

## Chunk 5: GitHub Actions CI Pipeline

### Dosyalar

- Create: `.github/workflows/ci.yml`

---

### Task 11: GitHub Actions CI workflow oluştur

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: `.github/workflows/` klasörünü oluştur ve `ci.yml` dosyasını ekle**

```yaml
# =============================================================================
# CI Pipeline — Build + Test
# =============================================================================
# Tetikleyiciler:
#   push → master         : Her commit'te çalışır
#   pull_request → master : Her PR'da çalışır, merge öncesi kontrol
#
# Job: build-and-test
#   → Tüm projeleri derler ve test suite'ini çalıştırır
#   → Test sonuçları artifact olarak yüklenir
# =============================================================================

name: CI

on:
  push:
    branches: [ "master" ]
  pull_request:
    branches: [ "master" ]

env:
  ASPNETCORE_ENVIRONMENT: Test
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build-and-test:
    name: Build & Test
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.x"

      - name: Restore dependencies
        run: dotnet restore Training-dotnet.slnx

      - name: Build
        run: dotnet build Training-dotnet.slnx --no-restore --configuration Release

      - name: Run tests
        run: |
          dotnet test Training-dotnet.slnx \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=test-results.trx" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./TestResults/**/*.trx

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: coverage-report
          path: ./TestResults/**/coverage.cobertura.xml
```

- [ ] **Step 2: YAML sözdizimini doğrula**

```powershell
# yamllint veya actionlint yoksa online doğrulayıcı kullanın
# En basit kontrol: dosyayı oku ve hatalı girintileme yok mu kontrol et
Get-Content .github/workflows/ci.yml
```

- [ ] **Step 3: Yerel test çalıştırması (CI ile aynı komut)**

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Test"
dotnet test Training-dotnet.slnx --configuration Release --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Beklenen: Tüm testler pass.

- [ ] **Step 4: Commit ve push**

```powershell
git add .github/workflows/ci.yml
git commit -m "ci: GitHub Actions CI pipeline eklendi (build + test)"
git push origin master
```

- [ ] **Step 5: GitHub Actions'da workflow'un tetiklendiğini doğrula**

GitHub reposunu aç → **Actions** sekmesi → `CI` workflow'unun çalıştığını gör.  
URL: `https://github.com/MertOmgen/training-dotnet/actions`

Beklenen: Yeşil tik (✓).

---

## Özet — Tamamlandığında Oluşan Yapı

```
.github/
  workflows/
    ci.yml                              ← GitHub Actions CI
tests/
  Catalog.Tests/
    Catalog.Tests.csproj
    Domain/
      BookTests.cs                      ← 5 unit test
    Application/
      CreateBookCommandHandlerTests.cs  ← 2 unit test
      CreateBookCommandValidatorTests.cs ← 5 unit test
      GetBookByIdQueryHandlerTests.cs   ← 2 unit test
    API/
      CatalogApiSmokeTests.cs           ← 1 smoke test
  Borrowing.Tests/
    Borrowing.Tests.csproj
    BorrowingApiSmokeTests.cs           ← 1 smoke test
  Identity.Tests/
    Identity.Tests.csproj
    IdentityApiSmokeTests.cs            ← 1 smoke test
  Notification.Tests/
    Notification.Tests.csproj
    NotificationApiSmokeTests.cs        ← 1 smoke test
src/.../Catalog.API/appsettings.Test.json
src/.../Borrowing.API/appsettings.Test.json
src/.../Identity.API/appsettings.Test.json
src/.../Notification.API/appsettings.Test.json
```

**Toplam: ~18 test, her PR'da otomatik çalışır.**
