using Catalog.Application.Commands;
using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

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
