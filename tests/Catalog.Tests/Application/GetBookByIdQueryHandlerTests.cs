using Catalog.Application.DTOs;
using Catalog.Application.Queries;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Catalog.Tests.Application;

/// <summary>
/// IAsyncCursor fake — FirstOrDefaultAsync gibi extension metodlari icin
/// gercek bir IAsyncCursor<T> implementasyonu saglar.
/// </summary>
internal sealed class FakeAsyncCursor<T> : IAsyncCursor<T>
{
    private readonly IEnumerable<T> _items;
    private bool _moved;

    public FakeAsyncCursor(IEnumerable<T> items) => _items = items;

    public IEnumerable<T> Current => _items;

    public bool MoveNext(CancellationToken cancellationToken = default)
    {
        if (_moved) return false;
        _moved = true;
        return _items.Any();
    }

    public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(MoveNext(cancellationToken));

    public void Dispose() { }
}

/// <summary>
/// GetBookByIdQueryHandler unit testleri.
/// Find() bir extension metot oldugu icin IMongoCollection.FindAsync() mock'lanir;
/// handler Find() cagirdiginda FindFluent icsel olarak FindAsync() cagirir.
/// </summary>
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
        // Find() extension -> FindFluent.ToCursorAsync() -> collection.FindAsync()
        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BookDto>>(),
                It.IsAny<FindOptions<BookDto, BookDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FakeAsyncCursor<BookDto>(Enumerable.Empty<BookDto>()));

        var result = await _handler.Handle(new GetBookByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Handle_WhenBookFound_ReturnsBookDto()
    {
        var bookId = Guid.NewGuid();
        var expectedDto = new BookDto(
            Id: bookId, Title: "Kar", Isbn: "9789750719387", Description: null,
            AuthorFirstName: "Orhan", AuthorLastName: "Pamuk", AuthorFullName: "Orhan Pamuk",
            PublishedYear: 2002, Category: "Roman", TotalCopies: 5, AvailableCopies: 5,
            IsActive: true, CreatedAt: DateTime.UtcNow, UpdatedAt: null);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BookDto>>(),
                It.IsAny<FindOptions<BookDto, BookDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FakeAsyncCursor<BookDto>(new[] { expectedDto }));

        var result = await _handler.Handle(new GetBookByIdQuery(bookId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(bookId);
        result.Title.Should().Be("Kar");
    }
}
