using SchoolLibrary.Services.Validation;

namespace SchoolLibrary.Tests;

public class IsbnTests
{
    [Theory]
    [InlineData("9780306406157")]
    [InlineData("978-0-306-40615-7")]
    [InlineData("0306406152")]
    [InlineData("0-306-40615-2")]
    [InlineData("080442957X")]
    [InlineData("080442957x")]
    public void Valid_isbn_is_accepted(string isbn)
    {
        Assert.True(Isbn.IsValid(isbn));
    }

    [Theory]
    [InlineData("9780306406158")]   // грешна контролна цифра (ISBN-13)
    [InlineData("0306406153")]      // грешна контролна цифра (ISBN-10)
    [InlineData("123")]             // твърде кратък
    [InlineData("")]
    [InlineData("ABCDEFGHIJ")]      // букви вместо цифри
    [InlineData("978030640615X")]   // X е позволено само в края на ISBN-10
    public void Invalid_isbn_is_rejected(string isbn)
    {
        Assert.False(Isbn.IsValid(isbn));
    }

    [Fact]
    public void Null_is_not_valid()
    {
        Assert.False(Isbn.IsValid(null));
    }

    [Fact]
    public void Normalize_removes_hyphens_and_spaces_and_uppercases()
    {
        Assert.Equal("080442957X", Isbn.Normalize(" 0-8044-2957-x "));
    }
}
