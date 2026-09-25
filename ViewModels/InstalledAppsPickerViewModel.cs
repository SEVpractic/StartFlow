using System.Collections.ObjectModel;
using StartFlow.Models;
using StartFlow.Services;

namespace StartFlow.ViewModels;

public sealed class InstalledApplicationItem : ObservableBase
{
    private readonly InstalledApplication _source;

    public InstalledApplicationItem(InstalledApplication source)
    {
        _source = source;
    }

    public InstalledApplication Source => _source;

    public string Name => _source.Name;

    public byte[]? IconData => _source.IconData;

    public bool CanAddAsProgram => _source.CanAddAsProgram;

    public string? RestrictionReason => _source.AddRestrictionReason;
}

public sealed class InstalledAppsPickerViewModel : ObservableBase
{
    private readonly InstalledApplicationsService _service;
    private readonly List<InstalledApplicationItem> _all = new();
    private string _searchText = string.Empty;
    private bool _isLoading;
    private InstalledApplicationItem? _selectedItem;

    public InstalledAppsPickerViewModel(InstalledApplicationsService? service = null)
    {
        _service = service ?? new InstalledApplicationsService();
    }

    public ObservableCollection<InstalledApplicationItem> Items { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetField(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool IsEmpty => _all.Count == 0;

    public bool HasNoResults => !IsLoading && Items.Count == 0;

    public InstalledApplicationItem? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var apps = await Task.Run(_service.GetInstalledApplications);
            SelectedItem = null;

            _all.Clear();
            var items = apps.Select(a => new InstalledApplicationItem(a)).ToList();
            items.Sort((x, y) => StringComparer.CurrentCultureIgnoreCase.Compare(x.Name, y.Name));
            _all.AddRange(items);

            OnPropertyChanged(nameof(IsEmpty));
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(i => i.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        Items.Clear();
        foreach (var item in matches)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(HasNoResults));
    }
}