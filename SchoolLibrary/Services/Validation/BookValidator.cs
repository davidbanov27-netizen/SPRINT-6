using SchoolLibrary.Models;

namespace SchoolLibrary.Services.Validation;

/// <summary>Правила за данните на книга (заглавие). Чиста логика – без достъп до файлове и екрани.</summary>
public static class BookValidator
{
    public const int MaxTitle = 150;
    public const int MaxAuthor = 100;
    public const int MaxGenre = 50;
    public const int MaxDescription = 500;
    public const int MaxShelf = 20;

    /// <param name="book">Книгата, която се проверява.</param>
    /// <param name="others">Всички ОСТАНАЛИ активни книги (без самата нея при редактиране).</param>
    public static List<ValidationError> Validate(Book book, IEnumerable<Book> others)
    {
        var errors = new List<ValidationError>();
        var title = (book.Title ?? "").Trim();
        var author = (book.Author ?? "").Trim();
        var genre = (book.Genre ?? "").Trim();
        var description = (book.Description ?? "").Trim();
        var shelf = (book.Shelf ?? "").Trim();
        var isbn = Isbn.Normalize(book.Isbn);
        var existing = others.ToList();

        if (title.Length == 0)
        {
            errors.Add(new(nameof(Book.Title), "Въведете заглавие на книгата."));
        }
        else if (title.Length > MaxTitle)
        {
            errors.Add(new(nameof(Book.Title), $"Заглавието е твърде дълго (най-много {MaxTitle} знака)."));
        }

        if (author.Length == 0)
        {
            errors.Add(new(nameof(Book.Author), "Въведете автор."));
        }
        else if (author.Length > MaxAuthor)
        {
            errors.Add(new(nameof(Book.Author), $"Името на автора е твърде дълго (най-много {MaxAuthor} знака)."));
        }

        if (genre.Length == 0)
        {
            errors.Add(new(nameof(Book.Genre), "Въведете жанр (напр. Роман, Приказка, Фентъзи)."));
        }
        else if (genre.Length > MaxGenre)
        {
            errors.Add(new(nameof(Book.Genre), $"Жанрът е твърде дълъг (най-много {MaxGenre} знака)."));
        }

        var minOk = book.MinGrade is >= 1 and <= 12;
        var maxOk = book.MaxGrade is >= 1 and <= 12;
        if (!minOk)
        {
            errors.Add(new(nameof(Book.MinGrade), "Класът „от“ трябва да е число от 1 до 12."));
        }

        if (!maxOk)
        {
            errors.Add(new(nameof(Book.MaxGrade), "Класът „до“ трябва да е число от 1 до 12."));
        }

        if (minOk && maxOk && book.MinGrade > book.MaxGrade)
        {
            errors.Add(new(nameof(Book.MinGrade), "Класът „от“ не може да е по-голям от класа „до“."));
        }

        if (isbn.Length > 0)
        {
            if (!Isbn.IsValid(isbn))
            {
                errors.Add(new(nameof(Book.Isbn),
                    $"ISBN „{book.Isbn.Trim()}“ не е валиден. Проверете цифрите – трябва да са 10 или 13."));
            }
            else
            {
                var sameIsbn = existing.FirstOrDefault(o => Isbn.Normalize(o.Isbn) == isbn);
                if (sameIsbn != null)
                {
                    errors.Add(new(nameof(Book.Isbn),
                        $"Книга с този ISBN вече съществува: „{sameIsbn.Title}“. Добавете ѝ екземпляр вместо нов запис."));
                }
            }
        }

        if (title.Length > 0 && author.Length > 0)
        {
            var sameBook = existing.FirstOrDefault(o =>
                string.Equals(o.Title.Trim(), title, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.Author.Trim(), author, StringComparison.OrdinalIgnoreCase));
            if (sameBook != null)
            {
                errors.Add(new(nameof(Book.Title),
                    $"Книгата „{title}“ от {author} вече съществува. Добавете ѝ още един екземпляр вместо нов запис."));
            }
        }

        if (description.Length > MaxDescription)
        {
            errors.Add(new(nameof(Book.Description), $"Описанието е твърде дълго (най-много {MaxDescription} знака)."));
        }

        if (shelf.Length > MaxShelf)
        {
            errors.Add(new(nameof(Book.Shelf), $"Рафтът е твърде дълъг (най-много {MaxShelf} знака)."));
        }

        return errors;
    }
}
