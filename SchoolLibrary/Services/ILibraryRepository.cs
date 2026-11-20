using SchoolLibrary.Models;

namespace SchoolLibrary.Services;

/// <summary>
/// Договор към хранилището. LibraryService знае само за този интерфейс,
/// затова JSON файлът (DataStore) може да се смени със SQLite или друга база
/// без промяна в бизнес правилата и в екраните.
/// </summary>
public interface ILibraryRepository
{
    /// <summary>Съобщение за последната грешка при четене или запис (null = всичко е наред).</summary>
    string? LastError { get; }

    LibraryData Load();

    void Save(LibraryData data);
}
