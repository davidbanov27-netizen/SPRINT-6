using System.Windows.Input;
using SchoolLibrary.Models;
using SchoolLibrary.Services;
using SchoolLibrary.Services.Validation;

namespace SchoolLibrary.ViewModels;

/// <summary>Екран „Читатели“: преглед, търсене, регистриране, редактиране и деактивиране.</summary>
public class ReadersViewModel : PageViewModel
{
    private readonly LibraryService _s;

    private string _searchText = "";
    private IReadOnlyList<ReaderRow> _readers = Array.Empty<ReaderRow>();
    private ReaderRow? _selectedReader;
    private IReadOnlyList<LoanRow> _selectedLoans = Array.Empty<LoanRow>();

    private bool _isFormOpen;
    private int? _editingId;
    private string _name = "";
    private string _className = "";
    private bool _isTeacher;
    private string _contact = "";
    private string _cardNumberText = "";

    public ReadersViewModel(LibraryService s)
    {
        _s = s;
        AddCommand = new RelayCommand(OpenAdd);
        EditCommand = new RelayCommand(OpenEdit, () => SelectedReader != null);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => { IsFormOpen = false; ClearMessage(); });
        ToggleActiveCommand = new RelayCommand(ToggleActive, () => SelectedReader != null);
        Refresh();
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    // ===== Списък =====

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public IReadOnlyList<ReaderRow> Readers
    {
        get => _readers;
        private set => Set(ref _readers, value);
    }

    public ReaderRow? SelectedReader
    {
        get => _selectedReader;
        set
        {
            if (Set(ref _selectedReader, value))
            {
                OnSelectionChanged();
            }
        }
    }

    public bool HasSelection => _selectedReader != null;

    public IReadOnlyList<LoanRow> SelectedLoans
    {
        get => _selectedLoans;
        private set => Set(ref _selectedLoans, value);
    }

    public bool HasSelectedLoans => _selectedLoans.Count > 0;
    public string ToggleActiveText => _selectedReader is { IsActive: false } ? "Активирай отново" : "Деактивирай";

    // ===== Форма =====

    public bool IsFormOpen
    {
        get => _isFormOpen;
        private set => Set(ref _isFormOpen, value);
    }

    public string FormHeading => _editingId == null ? "Нов читател" : "Редактиране на читател";

    public string Name { get => _name; set => Set(ref _name, value); }
    public string ClassName { get => _className; set => Set(ref _className, value); }
    public string Contact { get => _contact; set => Set(ref _contact, value); }

    public bool IsTeacher
    {
        get => _isTeacher;
        set
        {
            if (Set(ref _isTeacher, value))
            {
                OnPropertyChanged(nameof(IsStudent));
            }
        }
    }

    public bool IsStudent => !_isTeacher;

    /// <summary>Само за показване: читателският номер не се въвежда ръчно.</summary>
    public string CardNumberText { get => _cardNumberText; private set => Set(ref _cardNumberText, value); }

    // ===== Действия =====

    private void Refresh(int? selectId = null)
    {
        var keep = selectId ?? _selectedReader?.Id;
        Readers = _s.SearchReaders(_searchText).Select(r => RowFactory.ToReaderRow(_s, r)).ToList();
        SelectedReader = Readers.FirstOrDefault(r => r.Id == keep);
        OnPropertyChanged(nameof(SelectedReader));
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        SelectedLoans = _selectedReader == null
            ? Array.Empty<LoanRow>()
            : _s.LoansOf(_selectedReader.Reader).Select(l => RowFactory.ToLoanRow(_s, l)).ToList();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSelectedLoans));
        OnPropertyChanged(nameof(ToggleActiveText));
    }

    private void OpenAdd()
    {
        ClearMessage();
        _editingId = null;
        Name = "";
        ClassName = "";
        Contact = "";
        IsTeacher = false;
        CardNumberText = _s.NextCardNumber() + " (автоматичен)";
        OnPropertyChanged(nameof(FormHeading));
        IsFormOpen = true;
    }

    private void OpenEdit()
    {
        var row = SelectedReader;
        if (row == null)
        {
            return;
        }

        ClearMessage();
        var r = row.Reader;
        _editingId = r.Id;
        Name = r.Name;
        IsTeacher = r.Role == ReaderRole.Teacher;
        ClassName = IsTeacher ? "" : r.ClassName;
        Contact = r.Contact;
        CardNumberText = r.CardNumber;
        OnPropertyChanged(nameof(FormHeading));
        IsFormOpen = true;
    }

    private void Save()
    {
        var role = IsTeacher ? ReaderRole.Teacher : ReaderRole.Student;
        var cleanName = ReaderValidator.CleanName(Name);

        OperationResult result;
        int? selectId;
        string success;

        if (_editingId == null)
        {
            var added = _s.AddReader(Name, ClassName, role, Contact);
            result = added;
            selectId = added.Value?.Id;
            success = $"Читателят „{cleanName}“ е регистриран с номер {added.Value?.CardNumber}.";
        }
        else
        {
            var updated = _s.UpdateReader(_editingId.Value, Name, ClassName, role, Contact);
            result = updated;
            selectId = _editingId;
            success = $"Промените по „{cleanName}“ са запазени.";
        }

        if (!result.Success)
        {
            Show(MessageKind.Error, result.Summary);
            return;
        }

        IsFormOpen = false;
        Refresh(selectId);
        Show(MessageKind.Success, success);
    }

    private void ToggleActive()
    {
        var row = SelectedReader;
        if (row == null)
        {
            return;
        }

        var activate = !row.IsActive;
        var result = _s.SetReaderActive(row.Id, activate);
        if (!result.Success)
        {
            Show(MessageKind.Error, result.Summary);
            return;
        }

        Refresh(row.Id);
        Show(MessageKind.Success, activate
            ? $"Читателят „{row.Name}“ е активиран отново."
            : $"Читателят „{row.Name}“ е деактивиран.");
    }
}
