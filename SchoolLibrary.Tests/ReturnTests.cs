using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.Tests;

/// <summary>Етап 6: връщане – приключване на заема и освобождаване на екземпляра.</summary>
public class ReturnTests
{
    private static (LibraryService S, Book Book, Reader Reader, Loan Loan, BookCopy Copy) Lent(int copies = 1)
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: copies);
        var reader = s.AddReaderOk();
        var loan = s.Lend(reader, book).Value!;
        return (s, book, reader, loan, s.GetCopy(loan.CopyId)!);
    }

    [Fact]
    public void Return_ClosesLoan_AndFreesCopy()
    {
        var (s, book, reader, loan, copy) = Lent();

        var result = s.Return(loan, BookCondition.Good);

        Assert.True(result.Success, result.Summary);
        Assert.False(loan.IsActive);
        Assert.Equal(TestLibrary.Today, loan.ReturnedOn);
        Assert.Equal(BookCondition.Good, loan.Condition);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(1, s.Available(book));
        Assert.Empty(s.LoansOf(reader));
    }

    [Fact]
    public void Return_ResultDescribesWhatWasReturned()
    {
        var (s, book, reader, loan, copy) = Lent();

        var r = s.Return(loan, BookCondition.Good).Value!;

        Assert.Equal(book.Id, r.Book.Id);
        Assert.Equal(reader.Id, r.Reader.Id);
        Assert.Equal(copy.InventoryNumber, r.InventoryNumber);
        Assert.Equal(0, r.LateDays);
    }

    [Fact]
    public void Return_ThenCopyCanBeLentAgain_ToAnotherReader()
    {
        var (s, _, _, loan, copy) = Lent();
        var other = s.AddReaderOk("Мария Георгиева", "7Б");
        Assert.False(s.Lend(other, copy).Success); // още е зает

        s.Return(loan, BookCondition.Good);
        var again = s.Lend(other, copy);

        Assert.True(again.Success, again.Summary);
        Assert.Equal(copy.Id, again.Value!.CopyId);
        Assert.Equal(CopyStatus.OnLoan, copy.Status);
    }

    [Fact]
    public void Return_ReaderBelowLimitAgain()
    {
        var s = TestLibrary.Empty();
        var reader = s.AddReaderOk();
        var loans = new List<Loan>();
        for (var i = 1; i <= 3; i++)
        {
            loans.Add(s.Lend(reader, s.AddBookOk("Книга " + i, 1, "Автор " + i)).Value!);
        }

        var fourth = s.AddBookOk("Четвърта", 1, "Автор 4");
        Assert.False(s.Lend(reader, fourth).Success);

        s.Return(loans[0], BookCondition.Good);

        Assert.True(s.Lend(reader, fourth).Success);
    }

    [Fact]
    public void Return_Twice_SecondIsRefused_WithoutException()
    {
        var (s, _, _, loan, _) = Lent();
        s.Return(loan, BookCondition.Good);

        var second = s.Return(loan, BookCondition.Good);

        Assert.False(second.Success);
        Assert.Contains("вече е затворено", second.Summary);
    }

    [Fact]
    public void Return_Twice_DoesNotFreeAnotherReadersCopy()
    {
        var (s, _, _, loan, copy) = Lent();
        s.Return(loan, BookCondition.Good);
        var other = s.AddReaderOk("Мария Георгиева", "7Б");
        s.Lend(other, copy); // същият екземпляр при нов читател

        var stale = s.Return(loan, BookCondition.Good); // старият заем се връща пак

        Assert.False(stale.Success);
        Assert.Equal(CopyStatus.OnLoan, copy.Status);
    }

    [Fact]
    public void Return_Damaged_CopyIsNotAvailable()
    {
        var (s, book, _, loan, copy) = Lent(copies: 2);

        s.Return(loan, BookCondition.Damaged);

        Assert.Equal(CopyStatus.Damaged, copy.Status);
        Assert.Equal(1, s.Available(book));
        Assert.Equal(BookCondition.Damaged, loan.Condition);
    }

    [Fact]
    public void Return_Lost_CopyLeavesTotal()
    {
        var (s, book, _, loan, copy) = Lent(copies: 2);

        s.Return(loan, BookCondition.Lost);

        Assert.Equal(CopyStatus.Lost, copy.Status);
        Assert.Equal(1, s.TotalCopies(book));
    }

    [Fact]
    public void Return_Late_ReportsDays_AndLiftsBlock()
    {
        var clock = TestLibrary.Today;
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => clock);
        var reader = s.AddReaderOk();
        var loan = s.Lend(reader, s.AddBookOk()).Value!;

        clock = clock.AddDays(14 + 5); // 5 дни след срока

        var r = s.Return(loan, BookCondition.Good).Value!;

        Assert.Equal(5, r.LateDays);
        Assert.True(r.BlockLifted);
    }

    [Fact]
    public void Return_UnknownLoan_IsRefused()
    {
        var s = TestLibrary.Empty();

        var result = s.Return(new Loan { Id = 999 }, BookCondition.Good);

        Assert.False(result.Success);
        Assert.Contains("не е намерено", result.Summary);
    }

    [Fact]
    public void Return_IsSavedToRepository()
    {
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => TestLibrary.Today);
        var loan = s.Lend(s.AddReaderOk(), s.AddBookOk()).Value!;
        var before = repo.SaveCount;

        s.Return(loan, BookCondition.Good);

        Assert.Equal(before + 1, repo.SaveCount);
    }

    // ===== Връщане по инвентарен номер =====

    [Fact]
    public void ReturnByInventoryNumber_Works_CaseInsensitive()
    {
        var (s, _, _, loan, copy) = Lent();

        var result = s.Return(" " + copy.InventoryNumber.ToLowerInvariant() + " ", BookCondition.Good);

        Assert.True(result.Success, result.Summary);
        Assert.False(loan.IsActive);
        Assert.Equal(CopyStatus.Available, copy.Status);
    }

    [Fact]
    public void ReturnByInventoryNumber_Unknown_IsRefused()
    {
        var s = TestLibrary.Empty();

        var result = s.Return("ZZ-999", BookCondition.Good);

        Assert.False(result.Success);
        Assert.Contains("Няма екземпляр", result.Summary);
    }

    [Fact]
    public void ReturnByInventoryNumber_CopyNotOnLoan_IsRefused()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk();
        var copy = s.CopiesOf(book)[0];

        var result = s.Return(copy.InventoryNumber, BookCondition.Good);

        Assert.False(result.Success);
        Assert.Contains("не е зает", result.Summary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ReturnByInventoryNumber_Empty_IsRefused(string? number)
    {
        var s = TestLibrary.Empty();

        var result = s.Return(number, BookCondition.Good);

        Assert.False(result.Success);
        Assert.Contains("Въведете инвентарен номер", result.Summary);
    }

    [Fact]
    public void FindActiveLoan_ReturnsTheOpenLoan_OfTheCopy()
    {
        var (s, _, _, loan, copy) = Lent();

        var found = s.FindActiveLoan(copy.InventoryNumber);

        Assert.True(found.Success);
        Assert.Same(loan, found.Value);
    }

    // ===== Целият цикъл =====

    [Fact]
    public void FullCycle_Lend_Refuse_Return_Lend()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var ivan = s.AddReaderOk("Иван Петров", "7Б");
        var maria = s.AddReaderOk("Мария Георгиева", "7Б");

        var loan = s.Lend(ivan, book).Value!;                // 1. успешно заемане
        Assert.False(s.Lend(maria, book).Success);            // 2. екземплярът е зает
        Assert.True(s.Return(loan, BookCondition.Good).Success); // 3. връщане
        Assert.True(s.Lend(maria, book).Success);             // 4. отново е наличен

        Assert.Equal(2, s.AllLoans.Count);
        Assert.Single(s.ActiveLoans);
    }
}
