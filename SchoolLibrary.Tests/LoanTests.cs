using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.Tests;

/// <summary>Заемане и връщане след преминаването към реални екземпляри (регресия).</summary>
public class LoanTests
{
    /// <summary>Библиотека с часовник, който тестът може да превърта напред.</summary>
    private sealed class Clock
    {
        public DateTime Now { get; set; } = TestLibrary.Today;
    }

    private static (LibraryService Service, Clock Clock) WithClock()
    {
        var clock = new Clock();
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        return (new LibraryService(repo, () => clock.Now), clock);
    }

    [Fact]
    public void CreateLoan_TakesCopy_SetsDueDate14Days()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var reader = s.AddReaderOk();

        var loan = s.CreateLoan(reader, book);

        Assert.Equal(TestLibrary.Today, loan.LoanedOn);
        Assert.Equal(TestLibrary.Today.AddDays(14), loan.DueDate);
        Assert.Equal(CopyStatus.OnLoan, s.GetCopy(loan.CopyId)!.Status);
        Assert.Equal(1, s.Available(book));
    }

    [Fact]
    public void CreateLoan_TwoReaders_GetDifferentCopies()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var first = s.CreateLoan(s.AddReaderOk("Иван Петров", "7Б"), book);
        var second = s.CreateLoan(s.AddReaderOk("Мария Георгиева", "7Б"), book);

        Assert.NotEqual(first.CopyId, second.CopyId);
        Assert.Equal(0, s.Available(book));
    }

    [Fact]
    public void CreateLoan_NoFreeCopy_IsRefused()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        s.CreateLoan(s.AddReaderOk("Иван Петров", "7Б"), book);
        var other = s.AddReaderOk("Мария Георгиева", "7Б");

        Assert.Contains("Няма налично копие.", s.CheckLoan(other, book));
        Assert.Throws<InvalidOperationException>(() => s.CreateLoan(other, book));
    }

    [Fact]
    public void CreateLoan_DamagedCopyIsNotLent()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var copy = s.CopiesOf(book)[0];
        s.UpdateCopy(copy.Id, copy.InventoryNumber, CopyStatus.Damaged);

        Assert.Contains("Няма налично копие.", s.CheckLoan(s.AddReaderOk(), book));
    }

    [Fact]
    public void StudentLimit_IsThreeBooks()
    {
        var s = TestLibrary.Empty();
        var reader = s.AddReaderOk();
        for (var i = 1; i <= 3; i++)
        {
            s.CreateLoan(reader, s.AddBookOk("Книга " + i, 1, "Автор " + i));
        }

        var fourth = s.AddBookOk("Книга 4", 1, "Автор 4");

        Assert.Contains(s.CheckLoan(reader, fourth), p => p.Contains("лимит от 3"));
    }

    [Fact]
    public void TeacherLimit_IsFiveBooks()
    {
        var s = TestLibrary.Empty();
        var teacher = s.AddTeacherOk();
        for (var i = 1; i <= 5; i++)
        {
            s.CreateLoan(teacher, s.AddBookOk("Книга " + i, 1, "Автор " + i));
        }

        var sixth = s.AddBookOk("Книга 6", 1, "Автор 6");

        Assert.Contains(s.CheckLoan(teacher, sixth), p => p.Contains("лимит от 5"));
    }

    [Fact]
    public void ReferenceOnlyBook_CannotBeLent()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk("Речник", 1, "Колектив", referenceOnly: true);

        Assert.Contains(s.CheckLoan(s.AddReaderOk(), book), p => p.Contains("само за четене"));
    }

    [Fact]
    public void SameTitleTwice_IsRefused()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var reader = s.AddReaderOk();
        s.CreateLoan(reader, book);

        Assert.Contains("Читателят вече има това заглавие.", s.CheckLoan(reader, book));
    }

    [Fact]
    public void OverdueLoan_BlocksNewLoans_UntilReturned()
    {
        var (s, clock) = WithClock();
        var reader = s.AddReaderOk();
        var old = s.CreateLoan(reader, s.AddBookOk("Стара книга", 1, "А"));
        var next = s.AddBookOk("Нова книга", 1, "Б");

        clock.Now = clock.Now.AddDays(15); // срокът е изтекъл вчера
        Assert.Contains(s.CheckLoan(reader, next), p => p.Contains("просрочена"));

        var result = s.ReturnLoan(old, BookCondition.Good);

        Assert.Equal(1, result.LateDays);
        Assert.True(result.BlockLifted);
        Assert.Empty(s.CheckLoan(reader, next));
    }

    [Fact]
    public void ReturnLoan_OnTime_FreesCopy_NoLateDays()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var loan = s.CreateLoan(s.AddReaderOk(), book);

        var result = s.ReturnLoan(loan, BookCondition.Good);

        Assert.Equal(0, result.LateDays);
        Assert.False(result.BlockLifted);
        Assert.False(loan.IsActive);
        Assert.Equal(CopyStatus.Available, s.GetCopy(loan.CopyId)!.Status);
        Assert.Equal(1, s.Available(book));
    }

    [Fact]
    public void ReturnLoan_Damaged_CopyLeavesAvailability()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        var loan = s.CreateLoan(s.AddReaderOk(), book);

        s.ReturnLoan(loan, BookCondition.Damaged);

        Assert.Equal(CopyStatus.Damaged, s.GetCopy(loan.CopyId)!.Status);
        Assert.Equal(0, s.Available(book));
        Assert.Equal(1, s.TotalCopies(book));
    }

    [Fact]
    public void ReturnLoan_Lost_CopyLeavesTotal()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 2);
        var loan = s.CreateLoan(s.AddReaderOk(), book);

        s.ReturnLoan(loan, BookCondition.Lost);

        Assert.Equal(CopyStatus.Lost, s.GetCopy(loan.CopyId)!.Status);
        Assert.Equal(1, s.TotalCopies(book));
    }

    [Fact]
    public void ReturnLoan_Twice_Throws()
    {
        var s = TestLibrary.Empty();
        var loan = s.CreateLoan(s.AddReaderOk(), s.AddBookOk());
        s.ReturnLoan(loan, BookCondition.Good);

        Assert.Throws<InvalidOperationException>(() => s.ReturnLoan(loan, BookCondition.Good));
    }

    [Fact]
    public void DeactivateBook_WithCopyOnLoan_IsRefused()
    {
        var s = TestLibrary.Empty();
        var book = s.AddBookOk(copies: 1);
        s.CreateLoan(s.AddReaderOk(), book);

        var result = s.DeactivateBook(book);

        Assert.False(result.Success);
        Assert.Contains(book, s.Books);
    }

    // ===== Примерни данни =====

    [Fact]
    public void Seed_ReaderWithOverdue_CannotBorrow()
    {
        var s = TestLibrary.Seeded();
        var ivan = s.GetReader(101)!;
        var free = s.Books.First(b => !b.ReferenceOnly && s.Available(b) > 0 && !s.LoansOf(ivan).Any(l => l.BookId == b.Id));

        Assert.Contains(s.CheckLoan(ivan, free), p => p.Contains("просрочена"));
    }

    [Fact]
    public void Seed_ReaderAtLimit_CannotBorrow()
    {
        var s = TestLibrary.Seeded();
        var maria = s.GetReader(102)!;
        var free = s.Books.First(b => !b.ReferenceOnly && s.Available(b) > 0 && !s.LoansOf(maria).Any(l => l.BookId == b.Id));

        Assert.Contains(s.CheckLoan(maria, free), p => p.Contains("лимит"));
    }

    [Fact]
    public void Seed_LateReturn_Reports20Days_AndLiftsBlock()
    {
        var s = TestLibrary.Seeded();
        var georgi = s.GetReader(103)!;
        var loan = s.LoansOf(georgi).Single(s.IsOverdue);

        var result = s.ReturnLoan(loan, BookCondition.Good);

        Assert.Equal(20, result.LateDays);
        Assert.True(result.BlockLifted);
    }

    [Fact]
    public void Seed_Integrity_EveryActiveLoanHasOnLoanCopy_AndNumbersAreUnique()
    {
        var s = TestLibrary.Seeded();

        foreach (var loan in s.ActiveLoans)
        {
            var copy = s.GetCopy(loan.CopyId);
            Assert.NotNull(copy);
            Assert.Equal(CopyStatus.OnLoan, copy!.Status);
            Assert.Equal(loan.BookId, copy.BookId);
        }

        var numbers = s.Books.SelectMany(b => s.CopiesOf(b)).Select(c => c.InventoryNumber).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var cards = s.AllReaders.Select(r => r.CardNumber).ToList();
        Assert.Equal(cards.Count, cards.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
