using System.Windows.Input;
using SchoolLibrary.Models;
using SchoolLibrary.Services;

namespace SchoolLibrary.ViewModels;

/// <summary>UC-02 „Връщане“.</summary>
public class ReturnViewModel : PageViewModel
{
    private readonly LibraryService _s;

    private Reader? _reader;
    private IReadOnlyList<LoanRow> _loans = Array.Empty<LoanRow>();
    private LoanRow? _selectedLoan;
    private ConditionOption _selectedCondition;
    private string _inventoryInput = "";

    public ReturnViewModel(LibraryService s)
    {
        _s = s;
        Readers = s.Readers;
        Conditions = new[]
        {
            new ConditionOption("Добро", BookCondition.Good),
            new ConditionOption("Повредена", BookCondition.Damaged),
            new ConditionOption("Загубена", BookCondition.Lost),
        };
        _selectedCondition = Conditions[0];

        ConfirmCommand = new RelayCommand(Confirm, () => _selectedLoan != null);
        FindByNumberCommand = new RelayCommand(FindByNumber);
        CancelCommand = new RelayCommand(() =>
        {
            ClearMessage();
            SelectedReader = null;
        });
    }

    public IReadOnlyList<Reader> Readers { get; }
    public IReadOnlyList<ConditionOption> Conditions { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FindByNumberCommand { get; }

    /// <summary>Инвентарен номер за бързо връщане (като от четец на баркод).</summary>
    public string InventoryInput
    {
        get => _inventoryInput;
        set => Set(ref _inventoryInput, value ?? "");
    }

    public Reader? SelectedReader
    {
        get => _reader;
        set
        {
            if (Set(ref _reader, value))
            {
                ClearMessage();
                LoadLoans();
            }
        }
    }

    public IReadOnlyList<LoanRow> ActiveLoans
    {
        get => _loans;
        private set => Set(ref _loans, value);
    }

    public LoanRow? SelectedLoan
    {
        get => _selectedLoan;
        set
        {
            if (Set(ref _selectedLoan, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public ConditionOption SelectedCondition
    {
        get => _selectedCondition;
        set => Set(ref _selectedCondition, value ?? Conditions[0]);
    }

    public bool HasReader => _reader != null;
    public bool HasLoans => _loans.Count > 0;
    public bool NoLoans => _reader != null && _loans.Count == 0;
    public bool HasSelection => _selectedLoan != null;

    private void LoadLoans()
    {
        ActiveLoans = _reader == null
            ? Array.Empty<LoanRow>()
            : _s.LoansOf(_reader).OrderBy(l => l.DueDate).Select(l => RowFactory.ToLoanRow(_s, l)).ToList();
        SelectedLoan = null;
        SelectedCondition = Conditions[0];
        OnPropertyChanged(nameof(HasReader));
        OnPropertyChanged(nameof(HasLoans));
        OnPropertyChanged(nameof(NoLoans));
    }

    /// <summary>Намира заема по номера на екземпляра и го избира, за да остане само потвърждението.</summary>
    private void FindByNumber()
    {
        ClearMessage();
        var found = _s.FindActiveLoan(_inventoryInput);
        if (!found.Success)
        {
            Show(MessageKind.Error, found.Summary);
            return;
        }

        var loan = found.Value!;
        SelectedReader = _s.GetReader(loan.ReaderId);
        SelectedLoan = ActiveLoans.FirstOrDefault(r => r.Loan.Id == loan.Id);
        InventoryInput = "";
        OnPropertyChanged(nameof(InventoryInput));
    }

    private void Confirm()
    {
        var row = _selectedLoan;
        if (row == null)
        {
            return;
        }

        var outcome = _s.Return(row.Loan, _selectedCondition.Value);
        if (!outcome.Success)
        {
            Show(MessageKind.Error, outcome.Summary);
            LoadLoans();
            return;
        }

        var result = outcome.Value!;
        var text = $"Връщането е записано: „{result.Book.Title}“, екземпляр {result.InventoryNumber} ({_selectedCondition.Label}).";
        if (result.LateDays > 0)
        {
            text += $" Закъснение: {result.LateDays} дни.";
        }

        if (result.Condition != BookCondition.Good)
        {
            text += " Копието е изключено от наличността.";
        }
        else
        {
            text += " Екземплярът отново е наличен.";
        }

        if (result.BlockLifted)
        {
            text += " Блокировката на читателя е снета.";
        }

        LoadLoans();
        Show(result.LateDays > 0 ? MessageKind.Warning : MessageKind.Success, text);
    }
}
