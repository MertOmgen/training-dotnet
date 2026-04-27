using Catalog.Domain.Entities;
using Catalog.Domain.Events;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

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

        var book = result.Value!;
        book.DomainEvents.Should().ContainSingle(e => e is BookCreatedDomainEvent);
    }
}
