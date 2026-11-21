using System.IO;
using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.Tests;

/// <summary>Запис и четене на JSON файла във временна папка.</summary>
public sealed class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SchoolLibraryTests_" + Guid.NewGuid().ToString("N"));

    public PersistenceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Временна папка – не е проблем, ако не се изтрие.
        }
    }

    private string PathFor(string name) => Path.Combine(_dir, name);

    [Fact]
    public void RoundTrip_PreservesBooksCopiesReadersAndLoans()
    {
        var path = PathFor("library.json");
        var store = new DataStore(path);
        var service = new LibraryService(store, () => TestLibrary.Today);
        var book = service.AddBookOk("Нова книга", copies: 2);
        var reader = service.AddReaderOk("Петър Иванов", "9А");
        service.CreateLoan(reader, book);

        var reloaded = new LibraryService(new DataStore(path), () => TestLibrary.Today);

        Assert.Contains(reloaded.Books, b => b.Title == "Нова книга");
        var loadedBook = reloaded.Books.Single(b => b.Title == "Нова книга");
        Assert.Equal(2, reloaded.TotalCopies(loadedBook));
        Assert.Equal(1, reloaded.Available(loadedBook));
        Assert.Contains(reloaded.AllReaders, r => r.Name == "Петър Иванов" && r.CardNumber == reader.CardNumber);
        Assert.Single(reloaded.ActiveLoans, l => l.BookId == loadedBook.Id);
    }

    [Fact]
    public void Save_AfterEveryOperation_WritesToRepository()
    {
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => TestLibrary.Today);

        var book = s.AddBookOk();
        var afterBook = repo.SaveCount;
        s.AddReaderOk();

        Assert.True(afterBook >= 1);
        Assert.True(repo.SaveCount > afterBook);
    }

    [Fact]
    public void RejectedOperation_DoesNotSave()
    {
        var repo = new InMemoryRepository(new LibraryData { SchemaVersion = LibraryData.CurrentSchemaVersion });
        var s = new LibraryService(repo, () => TestLibrary.Today);
        s.AddBookOk();
        var before = repo.SaveCount;

        var result = s.AddReader("", "", ReaderRole.Student);

        Assert.False(result.Success);
        Assert.Equal(before, repo.SaveCount);
    }

    [Fact]
    public void MissingFile_LoadsSeedData()
    {
        var store = new DataStore(PathFor("missing.json"));

        var data = store.Load();

        Assert.Equal(LibraryData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.NotEmpty(data.Books);
        Assert.NotEmpty(data.Copies);
    }

    [Fact]
    public void OldSchemaFile_IsBackedUp_AndReplacedBySeed()
    {
        var path = PathFor("library.json");
        // Стар формат: без екземпляри и без SchemaVersion.
        File.WriteAllText(path, "{ \"Books\": [ { \"Id\": 1, \"Title\": \"Стара\", \"Author\": \"А\", \"Genre\": \"Р\" } ] }");
        var store = new DataStore(path);

        var data = store.Load();

        Assert.True(File.Exists(PathFor("library.v0.bak")));
        Assert.NotNull(store.LastError);
        Assert.Contains("по-стара версия", store.LastError);
        Assert.DoesNotContain(data.Books, b => b.Title == "Стара");
        Assert.NotEmpty(data.Copies);
    }

    [Fact]
    public void CorruptFile_FallsBackToSeed_WithMessage()
    {
        var path = PathFor("library.json");
        File.WriteAllText(path, "{ това не е json ");
        var store = new DataStore(path);

        var data = store.Load();

        Assert.NotEmpty(data.Books);
        Assert.NotNull(store.LastError);
    }
}
