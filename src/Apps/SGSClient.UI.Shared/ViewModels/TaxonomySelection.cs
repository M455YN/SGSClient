using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

/// <summary>One checkable genre/engine row.</summary>
public sealed partial class TaxonomyOption(TaxonomySelection owner, SgsGameTaxonomy.TaxonomyItem item) : ObservableObject
{
    public string Symbol { get; } = item.Symbol;
    public string Name { get; } = item.Name;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => owner.OnOptionChanged(this);
}

/// <summary>Multi-select over a fixed taxonomy list with a maximum number of picks.</summary>
public sealed class TaxonomySelection : ObservableObject
{
    private readonly string _emptyText;
    private readonly bool _filterMode;
    private bool _updating;

    /// <param name="filterMode">Filter pickers have no limit and show a compact "A, B +2" summary.</param>
    public TaxonomySelection(
        IEnumerable<SgsGameTaxonomy.TaxonomyItem> items,
        int max,
        string emptyText,
        bool filterMode = false)
    {
        Max = max;
        _filterMode = filterMode;
        _emptyText = emptyText;
        Options = new ObservableCollection<TaxonomyOption>(items.Select(i => new TaxonomyOption(this, i)));
    }

    public int Max { get; }

    /// <summary>Raised after the user (or <see cref="Set"/>) changed the selection.</summary>
    public event EventHandler? SelectionChanged;
    public ObservableCollection<TaxonomyOption> Options { get; }

    public IReadOnlyList<string> SelectedSymbols =>
        Options.Where(o => o.IsSelected).Select(o => o.Symbol).ToList();

    /// <summary>Header text for the picker, e.g. "Action, RPG" or the empty text.</summary>
    public string Summary
    {
        get
        {
            var names = Options.Where(o => o.IsSelected).Select(o => o.Name).ToList();
            if (names.Count == 0)
            {
                return _emptyText;
            }

            if (_filterMode)
            {
                return names.Count <= 2
                    ? string.Join(", ", names)
                    : $"{names[0]}, {names[1]} +{names.Count - 2}";
            }

            return $"{string.Join(", ", names)} ({names.Count}/{Max})";
        }
    }

    public void Set(IEnumerable<string>? symbols)
    {
        var wanted = new HashSet<string>(
            (symbols ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
        _updating = true;
        try
        {
            var picked = 0;
            foreach (var option in Options)
            {
                option.IsSelected = picked < Max && wanted.Contains(option.Symbol);
                if (option.IsSelected)
                {
                    picked++;
                }
            }
        }
        finally
        {
            _updating = false;
        }

        OnPropertyChanged(nameof(Summary));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void OnOptionChanged(TaxonomyOption changed)
    {
        if (_updating)
        {
            return;
        }

        if (changed.IsSelected && Options.Count(o => o.IsSelected) > Max)
        {
            _updating = true;
            try
            {
                changed.IsSelected = false;
            }
            finally
            {
                _updating = false;
            }
        }

        OnPropertyChanged(nameof(Summary));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
