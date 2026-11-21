using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.Tests;

/// <summary>Етап 6: заемане на конкретен екземпляр – успешно и отказано.</summary>
public class LendTests
{
    private static (LibraryService S, Book Book, BookCopy First, BookCopy Second) TwoCopies()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Под игото", copies: 2);
        var copies = s.CopiesOf(book);
        return (s, book, copies[0], copies[1]);
    }

    // ===== Успешно заемане =====

    [Fact]
    public void Lend_FreeCopy_Succeeds_AndSetsAllFields()
    {
        var (s, book, first, _) = TwoCopies();
        var reader = s.AddReaderOk();

        var result = s.Lend(reader, first);

        Assert.True(result.Success, result.Summary);
        var loan = result.Value!;
        Assert.Equal(first.Id, loan.CopyId);
        Assert.Equal(book.Id, loan.BookId);
        Assert.Equal(reader.Id, loan.ReaderId);
        Assert.Equal(TestLibrary.Today, loan.LoanedOn);
        Assert.Equal(TestLibrary.Today.AddDays(14), loan.DueDate);
        Assert.Null(loan.ReturnedOn);
        Assert.True(loan.IsActive);
        Assert.Equal(CopyStatus.OnLoan, first.Status);
        Assert.Contains(loan, s.LoansOf(reader));
    }

    [Fact]
    public void Lend_DueDate_FollowsSettings()
    {
        var (s, _, first, _) = TwoCopies();
        s.Settings.LoanDays = 7;

        var loan = s.Lend(s.AddReaderOk(), first).Value!;

        Assert.Equal(TestLibrary.Today.AddDays(7), loan.DueDate);
    }

    [Fact]
    public void Lend_ChosenSecondCopy_TakesThatCopy_NotTheFirst()
    {
        var (s, _, first, second) = TwoCopies();

        var loan = s.Lend(s.AddReaderOk(), second).Value!;

        Assert.Equal(second.Id, loan.CopyId);
        Assert.Equal(CopyStatus.Available, first.Status);
        Assert.Equal(CopyStatus.OnLoan, second.Status);
    }

    [Fact]
    public void Lend_ByBook_PicksLowestInventoryNumber()
    {
        var (s, book, first, _) = TwoCopies();

        var loan = s.Lend(s.AddReaderOk(), book).Value!;

        Assert.Equal(first.Id, loan.CopyId);
    }

    [Fact]
    public void Lend_ByBook_SkipsCopiesAlreadyOnLoan()
    {
        var (s, book, _, second) = TwoCopies();
        s.Lend(s.AddReaderOk("Иван Петров", "7Б"), book);

        var loan = s.Lend(s.AddReaderOk("Мария Георгиева", "7Б"), book).Value!;

        Assert.Equal(second.Id, loan.CopyId);
    }

    [Fact]
    public void Lend_Teacher_CanHoldFiveLoans()
    {
        var s = TestLibrary.Empty();
        var teacher = s.AddTeacherOk();
        for (var i = 1; i <= 5; i++)
        {
            var book = s.AddBookOk("Книга " + i, 1, "Автор " + i);
            Assert.True(s.Lend(teacher, book).Success);
        }

        Assert.Equal(5, s.LoansOf(teacher).Count);
    }

    [Fact]
    public void Lend_IsSavedToRepository()
    {
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => TestLibrary.Today);
        var book = s.AddBookOk();
        var reader = s.AddReaderOk();
        var before = repo.SaveCount;

        s.Lend(reader, book);

        Assert.Equal(before + 1, repo.SaveCount);
        Assert.Single(repo.Data.Loans);
    }

    // ===== Един екземпляр – два пъти =====

    [Fact]
    public void Lend_SameCopyTwice_SecondIsRefused()
    {
        var (s, _, first, _) = TwoCopies();
        var ivan = s.AddReaderOk("Иван Петров", "7Б");
        var maria = s.AddReaderOk("Мария Георгиева", "7Б");
        Assert.True(s.Lend(ivan, first).Success);

        var second = s.Lend(maria, first);

        Assert.False(second.Success);
        Assert.Contains("вече е зает", second.Summary);
        Assert.Single(s.ActiveLoans, l => l.CopyId == first.Id);
        Assert.Empty(s.LoansOf(maria));
    }

    [Fact]
    public void Lend_SameCopyToSameReaderTwice_IsRefused()
    {
        var (s, _, first, _) = TwoCopies();
        var reader = s.AddReaderOk();
        s.Lend(reader, first);

        var again = s.Lend(reader, first);

        Assert.False(again.Success);
        Assert.Single(s.ActiveLoans);
    }

    [Fact]
    public void Lend_WithStaleCopyObject_IsStillRefused()
    {
        // Екранът може да държи стара „снимка“ на екземпляра, в която той още е свободен.
        var (s, _, first, _) = TwoCopies();
        s.Lend(s.AddReaderOk("Иван Петров", "7Б"), first);
        var stale = new BookCopy { Id = first.Id, BookId = first.BookId, InventoryNumber = first.InventoryNumber, Status = CopyStatus.Available };

        var result = s.Lend(s.AddReaderOk("Мария Георгиева", "7Б"), stale);

        Assert.False(result.Success);
        Assert.Single(s.ActiveLoans);
    }

    [Fact]
    public void Lend_CopyMarkedAvailableButWithOpenLoan_IsRefused()
    {
        // Защита при повредени данни: незатворен заем има предимство пред състоянието.
        var (s, _, first, _) = TwoCopies();
        s.Lend(s.AddReaderOk("Иван Петров", "7Б"), first);
        first.Status = CopyStatus.Available;

        var result = s.Lend(s.AddReaderOk("Мария Георгиева", "7Б"), first);

        Assert.False(result.Success);
        Assert.Single(s.ActiveLoans);
    }

    [Fact]
    public void Lend_LastCopy_ThenNoMoreAvailable()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        s.Lend(s.AddReaderOk("Иван Петров", "7Б"), book);

        var result = s.Lend(s.AddReaderOk("Мария Георгиева", "7Б"), book);

        Assert.False(result.Success);
        Assert.Contains("Няма налично копие.", result.Summary);
    }

    // ===== Отказ заради екземпляра =====

    [Theory]
    [InlineData(CopyStatus.Damaged, "повреден")]
    [InlineData(CopyStatus.Lost, "загубен")]
    [InlineData(CopyStatus.Withdrawn, "списан")]
    public void Lend_UnavailableCopy_IsRefusedWithReason(CopyStatus status, string word)
    {
        var (s, _, first, _) = TwoCopies();
        s.UpdateCopy(first.Id, first.InventoryNumber, status);

        var result = s.Lend(s.AddReaderOk(), first);

        Assert.False(result.Success);
        Assert.Contains(word, result.Summary);
        Assert.Equal(status, first.Status);
        Assert.Empty(s.ActiveLoans);
    }

    [Fact]
    public void Lend_UnknownCopy_IsRefused()
    {
        var s = TestLibrary.Empty();
        var reader = s.AddReaderOk();

        var result = s.Lend(reader, new BookCopy { Id = 999, BookId = 1, InventoryNumber = "X-999" });

        Assert.False(result.Success);
        Assert.Contains("не е намерен", result.Summary);
    }

    [Fact]
    public void Lend_UnknownReader_IsRefused()
    {
        var (s, _, first, _) = TwoCopies();

        var result = s.Lend(new Reader { Id = 999, Name = "Непознат" }, first);

        Assert.False(result.Success);
        Assert.Equal(CopyStatus.Available, first.Status);
    }

    [Fact]
    public void Lend_CopyOfDeactivatedBook_IsRefused()
    {
        var (s, book, first, _) = TwoCopies();
        var reader = s.AddReaderOk();
        s.DeactivateBook(book);

        var result = s.Lend(reader, first);

        Assert.False(result.Success);
    }

    // ===== Отказ заради читателя =====

    [Fact]
    public void Lend_InactiveReader_IsRefused()
    {
        var (s, _, first, _) = TwoCopies();
        var reader = s.AddReaderOk();
        s.SetReaderActive(reader.Id, false);

        var result = s.Lend(reader, first);

        Assert.False(result.Success);
        Assert.Contains("деактивиран", result.Summary);
    }

    [Fact]
    public void Lend_ReaderAtLimit_IsRefused_AndCopyStaysFree()
    {
        var s = TestLibrary.Empty();
        var reader = s.AddReaderOk();
        for (var i = 1; i <= 3; i++)
        {
            Assert.True(s.Lend(reader, s.AddBookOk("Книга " + i, 1, "Автор " + i)).Success);
        }

        var extra = s.AddBookOk("Четвърта", 1, "Автор 4");
        var copy = s.CopiesOf(extra)[0];

        var result = s.Lend(reader, copy);

        Assert.False(result.Success);
        Assert.Contains("лимит от 3", result.Summary);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(3, s.LoansOf(reader).Count);
    }

    [Fact]
    public void Lend_ReaderWithOverdue_IsRefused_UntilReturn()
    {
        var s = TestLibrary.Seeded();
        var ivan = s.GetReader(101)!; // има просрочена книга
        var book = s.Books.First(b => !b.ReferenceOnly && s.Available(b) > 0 && !s.LoansOf(ivan).Any(l => l.BookId == b.Id));
        var copy = s.CopiesOf(book).First(c => c.Status == CopyStatus.Available);

        var refused = s.Lend(ivan, copy);
        Assert.False(refused.Success);
        Assert.Contains("просрочена", refused.Summary);

        foreach (var overdue in s.LoansOf(ivan).Where(s.IsOverdue).ToList())
        {
            Assert.True(s.Return(overdue, BookCondition.Good).Success);
        }

        Assert.True(s.Lend(ivan, copy).Success);
    }

    [Fact]
    public void Lend_ReferenceOnlyBook_IsRefused()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Речник", 1, "Колектив", referenceOnly: true);

        var result = s.Lend(s.AddReaderOk(), s.CopiesOf(book)[0]);

        Assert.False(result.Success);
        Assert.Contains("само за четене", result.Summary);
    }

    [Fact]
    public void Lend_SameTitleAnotherCopy_IsRefused()
    {
        var (s, _, first, second) = TwoCopies();
        var reader = s.AddReaderOk();
        s.Lend(reader, first);

        var result = s.Lend(reader, second);

        Assert.False(result.Success);
        Assert.Contains("вече има това заглавие", result.Summary);
        Assert.Equal(CopyStatus.Available, second.Status);
    }

    [Fact]
    public void Lend_MultipleReasons_AreAllListed()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Речник", 1, "Колектив", referenceOnly: true);
        var copy = s.CopiesOf(book)[0];
        var reader = s.AddReaderOk();
        s.SetReaderActive(reader.Id, false);

        var result = s.Lend(reader, copy);

        Assert.True(result.Errors.Count >= 2);
        Assert.StartsWith("•", result.Summary);
    }

    [Fact]
    public void Lend_Refused_ChangesNothing()
    {
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => TestLibrary.Today);
        var book = s.AddBookOk(copies: 1);
        var ivan = s.AddReaderOk("Иван Петров", "7Б");
        var maria = s.AddReaderOk("Мария Георгиева", "7Б");
        s.Lend(ivan, book);
        var saves = repo.SaveCount;

        var result = s.Lend(maria, book);

        Assert.False(result.Success);
        Assert.Equal(saves, repo.SaveCount);
        Assert.Single(repo.Data.Loans);
    }

    [Fact]
    public void CheckLoan_ForCopy_MatchesLendOutcome()
    {
        var (s, _, first, _) = TwoCopies();
        var reader = s.AddReaderOk();

        Assert.Empty(s.CheckLoan(reader, first));
        s.Lend(reader, first);
        Assert.NotEmpty(s.CheckLoan(s.AddReaderOk("Мария Георгиева", "7Б"), first));
    }

    [Fact]
    public void CreateLoan_Legacy_ThrowsWithSameMessage()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Речник", 1, "Колектив", referenceOnly: true);

        var ex = Assert.Throws<InvalidOperationException>(() => s.CreateLoan(s.AddReaderOk(), book));

        Assert.Contains("само за четене", ex.Message);
    }
}
