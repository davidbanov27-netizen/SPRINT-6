using System.Text.RegularExpressions;
using SchoolLibrary.Models;

namespace SchoolLibrary.Services.Validation;

/// <summary>Правила за инвентарния номер на екземпляр.</summary>
public static class CopyValidator
{
    // 3 до 20 знака: букви, цифри и - _ / . – без интервали, започва и свършва с буква или цифра.
    private static readonly Regex Pattern = new(@"^[\p{L}\p{N}][\p{L}\p{N}\-_/.]{1,18}[\p{L}\p{N}]$");

    /// <summary>Подрязва и прави буквите главни, за да не са различни „bk-1“ и „BK-1“.</summary>
    public static string Normalize(string? inventoryNumber) =>
        (inventoryNumber ?? "").Trim().ToUpperInvariant();

    /// <param name="inventoryNumber">Вече нормализираният номер.</param>
    /// <param name="others">Всички ОСТАНАЛИ екземпляри (без самия него при редактиране).</param>
    public static List<ValidationError> Validate(string inventoryNumber, IEnumerable<BookCopy> others)
    {
        var errors = new List<ValidationError>();

        if (inventoryNumber.Length == 0)
        {
            errors.Add(new(nameof(BookCopy.InventoryNumber), "Въведете инвентарен номер."));
            return errors;
        }

        if (!Pattern.IsMatch(inventoryNumber))
        {
            errors.Add(new(nameof(BookCopy.InventoryNumber),
                "Инвентарният номер трябва да е от 3 до 20 знака – букви, цифри и - _ / . (без интервали)."));
            return errors;
        }

        if (others.Any(c => string.Equals(c.InventoryNumber, inventoryNumber, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new(nameof(BookCopy.InventoryNumber),
                $"Инвентарен номер „{inventoryNumber}“ вече е зает от друг екземпляр. Изберете друг или оставете полето празно за автоматичен номер."));
        }

        return errors;
    }
}
