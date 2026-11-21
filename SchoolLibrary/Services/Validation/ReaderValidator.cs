using System.Text.RegularExpressions;
using SchoolLibrary.Models;

namespace SchoolLibrary.Services.Validation;

/// <summary>Правила за данните на читател.</summary>
public static class ReaderValidator
{
    public const int MinName = 2;
    public const int MaxName = 80;
    public const string TeacherClassName = "Учител";

    private static readonly Regex NamePattern = new(@"^\p{L}[\p{L} .'\-]*$");
    private static readonly Regex ClassPattern = new(@"^(1[0-2]|[1-9])[А-ЯA-Z]$");
    private static readonly Regex CardPattern = new(@"^[A-Z0-9\-]{3,15}$");
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");
    private static readonly Regex PhonePattern = new(@"^\+?\d{7,15}$");

    /// <summary>Едни и същи интервали, без излишни празни места по краищата.</summary>
    public static string CleanName(string? name) =>
        Regex.Replace((name ?? "").Trim(), @"\s+", " ");

    /// <summary>„7 б“ → „7Б“.</summary>
    public static string NormalizeClass(string? className) =>
        Regex.Replace((className ?? "").Trim(), @"\s+", "").ToUpperInvariant();

    public static string NormalizeCard(string? cardNumber) =>
        (cardNumber ?? "").Trim().ToUpperInvariant();

    /// <param name="reader">Читателят вече с почистени и нормализирани полета.</param>
    /// <param name="others">Всички ОСТАНАЛИ читатели (без самия него при редактиране).</param>
    public static List<ValidationError> Validate(Reader reader, IEnumerable<Reader> others)
    {
        var errors = new List<ValidationError>();
        var existing = others.ToList();

        // Име
        var name = reader.Name;
        if (name.Length == 0)
        {
            errors.Add(new(nameof(Reader.Name), "Въведете име на читателя."));
        }
        else if (name.Length < MinName)
        {
            errors.Add(new(nameof(Reader.Name), "Името е твърде късо."));
        }
        else if (name.Length > MaxName)
        {
            errors.Add(new(nameof(Reader.Name), $"Името е твърде дълго (най-много {MaxName} знака)."));
        }
        else if (!NamePattern.IsMatch(name))
        {
            errors.Add(new(nameof(Reader.Name), "Името може да съдържа само букви, интервали, тире и точка."));
        }

        // Клас (само за ученици)
        if (reader.Role == ReaderRole.Student && !ClassPattern.IsMatch(reader.ClassName))
        {
            errors.Add(new(nameof(Reader.ClassName),
                "Класът трябва да е номер от 1 до 12 и буква, напр. 7Б или 12А."));
        }

        // Контакт (по избор)
        var contact = reader.Contact.Trim();
        if (contact.Length > 0 && !IsValidContact(contact))
        {
            errors.Add(new(nameof(Reader.Contact),
                "Контактът трябва да е имейл (напр. име@домейн.bg) или телефон с 7 до 15 цифри."));
        }

        // Читателски номер
        if (!CardPattern.IsMatch(reader.CardNumber))
        {
            errors.Add(new(nameof(Reader.CardNumber),
                "Читателският номер трябва да е от 3 до 15 знака – главни букви, цифри и тире."));
        }
        else if (existing.Any(o => string.Equals(o.CardNumber, reader.CardNumber, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new(nameof(Reader.CardNumber), $"Читателски номер „{reader.CardNumber}“ вече е зает."));
        }

        // Дубликат по име и клас
        if (name.Length >= MinName)
        {
            var same = existing.FirstOrDefault(o =>
                string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.ClassName, reader.ClassName, StringComparison.OrdinalIgnoreCase));
            if (same != null)
            {
                errors.Add(new(nameof(Reader.Name),
                    $"Читател „{name}“ ({reader.ClassName}) вече е регистриран с номер {same.CardNumber}. " +
                    "Ако са двама различни души, добавете презиме или друго отличие."));
            }
        }

        return errors;
    }

    public static bool IsValidContact(string contact)
    {
        var trimmed = contact.Trim();
        if (EmailPattern.IsMatch(trimmed))
        {
            return true;
        }

        var digitsOnly = Regex.Replace(trimmed, @"[\s\-()]", "");
        return PhonePattern.IsMatch(digitsOnly);
    }
}
