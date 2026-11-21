using System.Text.Json.Serialization;

namespace SchoolLibrary.Models;

public enum ReaderRole { Student, Teacher }

public enum BookCondition { Good, Damaged, Lost }

/// <summary>Състояние на физическия екземпляр. „Зает“ се задава само със заемане.</summary>
public enum CopyStatus { Available, OnLoan, Damaged, Lost, Withdrawn }

/// <summary>Заглавие в каталога. Физическите книги са екземпляри (<see cref="BookCopy"/>).</summary>
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Isbn { get; set; } = "";
    public int MinGrade { get; set; } = 1;
    public int MaxGrade { get; set; } = 12;
    public string Description { get; set; } = "";
    public string Shelf { get; set; } = "";

    /// <summary>BR-04: справочни издания и книги „само за четене в библиотеката“.</summary>
    public bool ReferenceOnly { get; set; }

    /// <summary>FR-02: книгата се деактивира, а не се трие, за да остане историята на заеманията.</summary>
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public string AgeText => MinGrade <= 1 && MaxGrade >= 12 ? "всички класове" : $"{MinGrade}–{MaxGrade} клас";
}

/// <summary>Един физически екземпляр на книга с уникален инвентарен номер.</summary>
public class BookCopy
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string InventoryNumber { get; set; } = "";
    public CopyStatus Status { get; set; } = CopyStatus.Available;
}

public class Reader
{
    public int Id { get; set; }

    /// <summary>Уникален читателски номер (напр. RD-0101). Генерира се автоматично.</summary>
    public string CardNumber { get; set; } = "";

    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public ReaderRole Role { get; set; } = ReaderRole.Student;

    /// <summary>По избор: имейл или телефон. Събира се само ако е нужно (NFR-04).</summary>
    public string Contact { get; set; } = "";

    /// <summary>BR-01: заема се само при активна регистрация.</summary>
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public string DisplayName => Role == ReaderRole.Teacher
        ? $"{CardNumber} – {Name} (учител)"
        : $"{CardNumber} – {Name} ({ClassName})";

    /// <summary>Номерът на класа („7Б“ → 7). За учители е null.</summary>
    [JsonIgnore]
    public int? Grade
    {
        get
        {
            var digits = new string(ClassName.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var grade) ? grade : null;
        }
    }
}

public class Loan
{
    public int Id { get; set; }

    /// <summary>Кой точно екземпляр е зает.</summary>
    public int CopyId { get; set; }

    /// <summary>Заглавието на екземпляра (запазено тук, за да са прости справките).</summary>
    public int BookId { get; set; }

    public int ReaderId { get; set; }
    public DateTime LoanedOn { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedOn { get; set; }
    public BookCondition? Condition { get; set; }

    [JsonIgnore]
    public bool IsActive => ReturnedOn == null;
}

/// <summary>NFR-09: правилата са настройки, а не са вградени в логиката.</summary>
public class LibrarySettings
{
    public int LoanDays { get; set; } = 14;
    public int StudentLimit { get; set; } = 3;
    public int TeacherLimit { get; set; } = 5;
    public int ReviewAfterDays { get; set; } = 30;
}

public class LibraryData
{
    /// <summary>Версия на формата. Файл с по-стара версия не се чете, а се архивира.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; }
    public List<Book> Books { get; set; } = new();
    public List<BookCopy> Copies { get; set; } = new();
    public List<Reader> Readers { get; set; } = new();
    public List<Loan> Loans { get; set; } = new();
    public LibrarySettings Settings { get; set; } = new();
}
