using System.IO;
using System.Text.Json;
using SchoolLibrary.Models;

namespace SchoolLibrary.Services;

/// <summary>
/// Записва данните в JSON файл в %LocalAppData%\SchoolLibrary\library.json.
/// По-късно може да се замени със SQLite само чрез нов клас по <see cref="ILibraryRepository"/>.
/// </summary>
public class DataStore : ILibraryRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public DataStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolLibrary",
            "library.json");
    }

    public string FilePath => _path;

    /// <summary>Съобщение за последната грешка или важна забележка (null = всичко е наред).</summary>
    public string? LastError { get; private set; }

    public LibraryData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var data = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(_path), Options);
                if (data != null && data.SchemaVersion == LibraryData.CurrentSchemaVersion && data.Books.Count > 0)
                {
                    return data;
                }

                // Файл от по-стара версия (преди екземплярите): пазим го като копие и почваме с чисти данни.
                if (data != null && data.SchemaVersion < LibraryData.CurrentSchemaVersion)
                {
                    var backup = Path.ChangeExtension(_path, ".v" + data.SchemaVersion + ".bak");
                    File.Copy(_path, backup, overwrite: true);
                    LastError = "Файлът с данни е от по-стара версия. Копие е запазено като " + backup + ".";
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LastError = "Файлът с данни не може да се прочете. Заредени са примерни данни. " + ex.Message;
        }

        return SeedData.Create(DateTime.Today);
    }

    public void Save(LibraryData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = "Данните не са записани: " + ex.Message;
        }
    }
}
