using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.Tests;

/// <summary>Екземпляри и уникален инвентарен номер.</summary>
public class CopyTests
{
    [Fact]
    public void AddBook_CreatesRequestedCopies_WithSequentialAutoNumbers()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Под игото", copies: 3);

        var numbers = s.CopiesOf(book).Select(c => c.InventoryNumber).ToList();
        Assert.Equal(new[] { "BK-00001", "BK-00002", "BK-00003" }, numbers);
        Assert.Equal(3, s.Available(book));
    }

    [Fact]
    public void AddCopy_WithoutNumber_GetsNextAutoNumber()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Под игото", copies: 1);

        var result = s.AddCopy(book);

        Assert.True(result.Success, result.Summary);
        Assert.Equal("BK-00002", result.Value!.InventoryNumber);
        Assert.Equal(2, s.TotalCopies(book));
    }

    [Fact]
    public void AddCopy_ManualNumber_IsNormalizedToUpperCase()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk();

        var result = s.AddCopy(book, "  lit-77 ");

        Assert.True(result.Success, result.Summary);
        Assert.Equal("LIT-77", result.Value!.InventoryNumber);
    }

    [Fact]
    public void AddCopy_DuplicateNumber_IsRejected()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1); // BK-00001

        var result = s.AddCopy(book, "BK-00001");

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.InventoryNumber)));
        Assert.Contains("вече е зает", result.Summary);
        Assert.Equal(1, s.TotalCopies(book));
    }

    [Fact]
    public void AddCopy_DuplicateNumber_IsCaseInsensitive_AndAcrossBooks()
    {
        var s = TestLibrary.Empty();
        var first = s.AddBookOk("Първа", copies: 1);
        var second = s.AddBookOk("Втора", copies: 1);

        var result = s.AddCopy(second, "bk-00001");

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.InventoryNumber)));
        Assert.Equal(1, s.TotalCopies(first));
    }

    [Theory]
    [InlineData("AB")]                      // твърде къс
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]   // 21 знака
    [InlineData("BK 001")]                  // интервал
    [InlineData("-BK001")]                  // започва с тире
    [InlineData("BK001-")]                  // завършва с тире
    [InlineData("BK#001")]                  // непозволен знак
    public void AddCopy_InvalidFormat_IsRejected(string number)
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk();

        var result = s.AddCopy(book, number);

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.InventoryNumber)));
    }

    [Fact]
    public void AddBook_WithMoreThanMaxCopies_IsRejected()
    {
        var s = TestLibrary.Empty();

        var result = s.AddBook(TestLibrary.Draft(), LibraryService.MaxInitialCopies + 1);

        Assert.False(result.Success);
        Assert.True(result.HasError("Copies"));
        Assert.Empty(s.Books);
    }

    [Fact]
    public void AddBook_WithNegativeCopies_IsRejected()
    {
        var s = TestLibrary.Empty();

        var result = s.AddBook(TestLibrary.Draft(), -1);

        Assert.False(result.Success);
        Assert.True(result.HasError("Copies"));
    }

    [Fact]
    public void AddCopy_ToUnknownBook_IsRejected()
    {
        var s = TestLibrary.Empty();

        var result = s.AddCopy(new Book { Id = 999, Title = "Няма ме" });

        Assert.False(result.Success);
        Assert.True(result.HasError("Id"));
    }

    [Fact]
    public void UpdateCopy_ChangesNumberAndStatus()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var copy = s.CopiesOf(book)[0];

        var result = s.UpdateCopy(copy.Id, "NEW-001", CopyStatus.Damaged);

        Assert.True(result.Success, result.Summary);
        Assert.Equal("NEW-001", copy.InventoryNumber);
        Assert.Equal(CopyStatus.Damaged, copy.Status);
        Assert.Equal(1, s.Available(book)); // повреденият не е наличен
    }

    [Fact]
    public void UpdateCopy_KeepingOwnNumber_IsNotADuplicate()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var copy = s.CopiesOf(book)[0];

        var result = s.UpdateCopy(copy.Id, copy.InventoryNumber, CopyStatus.Withdrawn);

        Assert.True(result.Success, result.Summary);
    }

    [Fact]
    public void UpdateCopy_NumberOfAnotherCopy_IsRejected()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var copies = s.CopiesOf(book);

        var result = s.UpdateCopy(copies[1].Id, copies[0].InventoryNumber, CopyStatus.Available);

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.InventoryNumber)));
        Assert.Equal("BK-00002", copies[1].InventoryNumber);
    }

    [Fact]
    public void UpdateCopy_OnLoanCopy_IsRejected()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var reader = s.AddReaderOk();
        s.CreateLoan(reader, book);
        var copy = s.CopiesOf(book)[0];

        var result = s.UpdateCopy(copy.Id, "NEW-001", CopyStatus.Available);

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.Status)));
        Assert.Equal(CopyStatus.OnLoan, copy.Status);
    }

    [Fact]
    public void UpdateCopy_CannotSetOnLoanManually()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var copy = s.CopiesOf(book)[0];

        var result = s.UpdateCopy(copy.Id, copy.InventoryNumber, CopyStatus.OnLoan);

        Assert.False(result.Success);
        Assert.True(result.HasError(nameof(BookCopy.Status)));
    }

    [Fact]
    public void LostAndWithdrawnCopies_AreNotCountedInTotal()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 4);
        var copies = s.CopiesOf(book);
        s.UpdateCopy(copies[0].Id, copies[0].InventoryNumber, CopyStatus.Lost);
        s.UpdateCopy(copies[1].Id, copies[1].InventoryNumber, CopyStatus.Withdrawn);
        s.UpdateCopy(copies[2].Id, copies[2].InventoryNumber, CopyStatus.Damaged);

        Assert.Equal(2, s.TotalCopies(book)); // Damaged + Available
        Assert.Equal(1, s.Available(book));
    }

    [Fact]
    public void NextInventoryNumber_SkipsManualNumbersThatDoNotMatchPattern()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        s.AddCopy(book, "LIT-77");

        Assert.Equal("BK-00002", s.NextInventoryNumber());
    }
}
