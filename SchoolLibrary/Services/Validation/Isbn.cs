namespace SchoolLibrary.Services.Validation;

/// <summary>Проверка на ISBN-10 и ISBN-13 по контролната цифра.</summary>
public static class Isbn
{
    /// <summary>Маха тирета и интервали и прави буквите главни (за „x“ в края на ISBN-10).</summary>
    public static string Normalize(string? input) =>
        new string((input ?? "").Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool IsValid(string? input)
    {
        var s = Normalize(input);
        return s.Length switch
        {
            10 => IsValid10(s),
            13 => IsValid13(s),
            _ => false,
        };
    }

    private static bool IsValid10(string s)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            int value;
            if (i == 9 && s[i] == 'X')
            {
                value = 10;
            }
            else if (char.IsAsciiDigit(s[i]))
            {
                value = s[i] - '0';
            }
            else
            {
                return false;
            }

            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    private static bool IsValid13(string s)
    {
        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            if (!char.IsAsciiDigit(s[i]))
            {
                return false;
            }

            sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }
}
