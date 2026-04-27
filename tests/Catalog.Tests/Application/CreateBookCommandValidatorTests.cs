using Catalog.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

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
