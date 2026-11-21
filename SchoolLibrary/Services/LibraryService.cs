using SchoolLibrary.Models;

namespace SchoolLibrary.Services;

public sealed record ReturnResult(Book Book, Reader Reader, BookCondition Condition, int LateDays, bool BlockLifted);

/// <summary>
/// Цялата бизнес логика на библиотеката (BR-01 … BR-15).
/// Класът е разделен на три файла: този (четене и заемане/връщане),
/// <c>LibraryService.Books.cs</c> (книги и екземпляри) и <c>LibraryService.Readers.cs</c> (читатели).
/// Екраните не пазят правила – те само извикват този клас.
/// </summary>
public partial class LibraryService
{
    private static readonly char[] Separators =
        { ' ', '\t', ',', '.', ';', ':', '-', '–', '"', '„', '“', '(', ')', '!', '?' };

    private readonly ILibraryRepository _store;
    private readonly LibraryData _data;
    private readonly Func<DateTime> _clock;

    /// <param name="clock">Часовник. По подразбиране е днешната дата; в тестове се подава фиксирана.</param>
    public LibraryService(ILibraryRepository store, Func<DateTime>? clock = null)
    {
        _store = store;
        _clock = clock ?? (() => DateTime.Today);
        _data = store.Load();
    }

    public DateTime Today => _clock().Date;
    public LibrarySettings Settings => _data.Settings;
    public string? LastSaveError => _store.LastError;

    // ===== Четене =====

    public IReadOnlyList<Book> Books => _data.Books.Where(b => b.IsActive).OrderBy(b => b.Title).ToList();

    /// <summary>Активните читатели – за избор при заемане, връщане и вход.</summary>
    public IReadOnlyList<Reader> Readers => _data.Readers.Where(r => r.IsActive).OrderBy(r => r.CardNumber).ToList();

    /// <summary>Всички читатели, включително деактивираните – за екрана „Читатели“.</summary>
    public IReadOnlyList<Reader> AllReaders => _data.Readers.OrderBy(r => r.CardNumber).ToList();

    public IReadOnlyList<Loan> AllLoans => _data.Loans;
    public IEnumerable<Loan> ActiveLoans => _data.Loans.Where(l => l.IsActive);

    public Book? GetBook(int id) => _data.Books.FirstOrDefault(b => b.Id == id);
    public Reader? GetReader(int id) => _data.Readers.FirstOrDefault(r => r.Id == id);
    public BookCopy? GetCopy(int id) => _data.Copies.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<BookCopy> CopiesOf(Book book) =>
        _data.Copies.Where(c => c.BookId == book.Id).OrderBy(c => c.InventoryNumber).ToList();

    /// <summary>Екземпляри, които могат да се заемат в момента.</summary>
    public int Available(Book book) =>
        _data.Copies.Count(c => c.BookId == book.Id && c.Status == CopyStatus.Available);

    /// <summary>Екземпляри, с които библиотеката разполага (без загубените и списаните).</summary>
    public int TotalCopies(Book book) =>
        _data.Copies.Count(c => c.BookId == book.Id && c.Status is not (CopyStatus.Lost or CopyStatus.Withdrawn));

    public IReadOnlyList<Loan> LoansOf(Reader reader) => ActiveLoans.Where(l => l.ReaderId == reader.Id).ToList();

    public bool IsOverdue(Loan loan) => loan.IsActive && loan.DueDate.Date < Today;
    public int DaysOverdue(Loan loan) => IsOverdue(loan) ? (Today - loan.DueDate.Date).Days : 0;

    public int LimitOf(Reader reader) =>
        reader.Role == ReaderRole.Teacher ? Settings.TeacherLimit : Settings.StudentLimit;

    // ===== Заемане (BR-01 … BR-06) =====

    /// <summary>Връща причините, поради които заемането не е позволено. Празен списък = позволено.</summary>
    public IReadOnlyList<string> CheckLoan(Reader reader, Book book)
    {
        var problems = new List<string>();

        if (!reader.IsActive)
        {
            problems.Add("Читателят е деактивиран и не може да заема книги.");
        }

        if (book.ReferenceOnly)
        {
            problems.Add("Книгата е само за четене в библиотеката и не може да се изнася.");
        }

        if (Available(book) < 1)
        {
            problems.Add("Няма налично копие.");
        }

        var loans = LoansOf(reader);
        var limit = LimitOf(reader);
        if (loans.Count >= limit)
        {
            problems.Add($"Достигнат лимит от {limit} книги.");
        }

        var overdue = loans.FirstOrDefault(IsOverdue);
        if (overdue != null)
        {
            problems.Add($"Читателят има просрочена книга: „{GetBook(overdue.BookId)?.Title}“ " +
                         $"(срок {overdue.DueDate:dd.MM.yyyy}). Върнете я, за да заемете нова.");
        }

        if (loans.Any(l => l.BookId == book.Id))
        {
            problems.Add("Читателят вече има това заглавие.");
        }

        return problems;
    }

    public Loan CreateLoan(Reader reader, Book book)
    {
        var problems = CheckLoan(reader, book);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }

        // Първият свободен екземпляр по инвентарен номер.
        var copy = _data.Copies
            .Where(c => c.BookId == book.Id && c.Status == CopyStatus.Available)
            .OrderBy(c => c.InventoryNumber)
            .First();
        copy.Status = CopyStatus.OnLoan;

        var loan = new Loan
        {
            Id = NextId(_data.Loans.Select(l => l.Id)),
            CopyId = copy.Id,
            BookId = book.Id,
            ReaderId = reader.Id,
            LoanedOn = Today,
            DueDate = Today.AddDays(Settings.LoanDays),
        };
        _data.Loans.Add(loan);
        Save();
        return loan;
    }

    // ===== Връщане (BR-07, BR-09, BR-11, BR-13, BR-15) =====

    public ReturnResult ReturnLoan(Loan loan, BookCondition condition)
    {
        if (!loan.IsActive)
        {
            throw new InvalidOperationException("Заемането вече е затворено.");
        }

        var book = GetBook(loan.BookId) ?? throw new InvalidOperationException("Книгата не е намерена.");
        var reader = GetReader(loan.ReaderId) ?? throw new InvalidOperationException("Читателят не е намерен.");
        var copy = GetCopy(loan.CopyId) ?? throw new InvalidOperationException("Екземплярът не е намерен.");

        var late = DaysOverdue(loan); // преди да затворим заемането

        loan.ReturnedOn = Today;
        loan.Condition = condition;

        // Повреденият или загубеният екземпляр не се връща в наличността (решението за обезщетение е извън системата).
        copy.Status = condition switch
        {
            BookCondition.Damaged => CopyStatus.Damaged,
            BookCondition.Lost => CopyStatus.Lost,
            _ => CopyStatus.Available,
        };

        var lifted = late > 0 && !LoansOf(reader).Any(IsOverdue);
        Save();
        return new ReturnResult(book, reader, condition, late, lifted);
    }

    // ===== Търсене по думи (FR-18) =====

    /// <summary>Търсене по заглавие, автор, жанр и описание с толерантност към една правописна грешка.</summary>
    public IReadOnlyList<Book> SearchKeyword(string query)
    {
        var terms = Tokenize(query);
        if (terms.Count == 0)
        {
            return Books;
        }

        return Books.Where(book =>
        {
            var words = Tokenize($"{book.Title} {book.Author} {book.Genre} {book.Description}");
            return terms.All(term => words.Any(word => Matches(word, term)));
        }).ToList();
    }

    private static bool Matches(string word, string term) =>
        word.Contains(term) || (term.Length >= 4 && Levenshtein(word, term) <= 1);

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Разстояние на Левенщайн: колко единични промени делят две думи.</summary>
    public static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    // ===== Помощни =====

    private static int NextId(IEnumerable<int> ids) => ids.DefaultIfEmpty(0).Max() + 1;

    private void Save() => _store.Save(_data);
}
