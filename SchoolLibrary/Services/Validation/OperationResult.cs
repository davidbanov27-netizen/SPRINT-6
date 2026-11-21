namespace SchoolLibrary.Services.Validation;

/// <summary>Една грешка: в кое поле е и какво да направи потребителят.</summary>
public sealed record ValidationError(string Field, string Message);

/// <summary>
/// Резултат от операция върху данните. Вместо изключения за „нормални“ грешки
/// (празно заглавие, дублиран номер) услугата връща списък с разбираеми съобщения.
/// </summary>
public class OperationResult
{
    public OperationResult(IEnumerable<ValidationError>? errors = null)
    {
        Errors = errors?.ToList() ?? new List<ValidationError>();
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool Success => Errors.Count == 0;

    /// <summary>Едно съобщение, ако е една грешка, иначе списък с точки.</summary>
    public string Summary => Errors.Count switch
    {
        0 => "",
        1 => Errors[0].Message,
        _ => string.Join(Environment.NewLine, Errors.Select(e => "• " + e.Message)),
    };

    public bool HasError(string field) => Errors.Any(e => e.Field == field);

    public static OperationResult Ok() => new();

    public static OperationResult Fail(string field, string message) =>
        new(new[] { new ValidationError(field, message) });
}

public sealed class OperationResult<T> : OperationResult
{
    private OperationResult(T? value, IEnumerable<ValidationError>? errors) : base(errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static OperationResult<T> Ok(T value) => new(value, null);

    public static OperationResult<T> Fail(IEnumerable<ValidationError> errors) => new(default, errors);

    public static new OperationResult<T> Fail(string field, string message) =>
        new(default, new[] { new ValidationError(field, message) });
}
