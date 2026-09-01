using System.ComponentModel;
using System.Runtime.CompilerServices;

using PoeRedux.Patches;
using PoeRedux.Services;

namespace PoeRedux.Models;

/// <summary>
/// One tab of the folder picker: a patch, the tree it can be pointed at, and what it says while
/// the game files are still being read. The two sweeps stay separate because only 58% of effect
/// folders have a particle folder of the same name, so one merged row would half the time drive
/// nothing.
/// </summary>
public class EffectSweepViewModel(ISelectiveSweep sweep, string header, IReadOnlyCollection<string> defaults)
    : INotifyPropertyChanged
{
    public string Header { get; } = header;

    public ISelectiveSweep Sweep { get; } = sweep;

    /// <summary>The patch's own protect list, so Defaults can put it back.</summary>
    public IReadOnlyCollection<string> Defaults { get; } = defaults;

    public EffectFolderViewModel? Root { get; private set; }

    public string Status { get; private set; } = "Reading the game files...";

    public bool IsLoading => Root is null;

    public void Load(EffectTargets.Folder folder)
    {
        Root = new EffectFolderViewModel(folder, null);
        Root.Uncheck([.. Sweep.DisabledPrefixes]);
        Status = $"{Root.FolderCount} folders";
        Changed(nameof(Root), nameof(Status), nameof(IsLoading));
    }

    public void Fail(string message)
    {
        Status = message;
        Changed(nameof(Status));
    }

    /// <summary>
    /// Filters the tree and reports the count. An empty result has to say so: half of the monster
    /// families are not named after their effects, and a blank tree otherwise reads as
    /// "this monster has no effects".
    /// </summary>
    public void Filter(MonsterNames.Match query)
    {
        if (Root is null)
            return;

        foreach (var child in Root.Children)
            child.Filter(query.Needle, query.Folders);

        var shown = Root.Descendants().Count(f => f.IsVisible);
        Status = query.Query.Length == 0 ? $"{Root.FolderCount} folders"
            : shown == 0 ? "no folder matches that name, browse the list instead"
            : query.Name is not null ? $"{query.Name} is a {query.Family}, {shown} folders"
            : query.Resolved ? $"{query.Family}, {shown} folders"
            : $"{shown} folders match";
        Changed(nameof(Status));
    }

    public void SetAll(bool ticked)
    {
        foreach (var child in Root?.Children ?? [])
            child.IsChecked = ticked;
    }

    public void Reset()
    {
        SetAll(true);
        Root?.Uncheck(Defaults);
    }

    /// <summary>Hands the selection back to the patch. A tab that never loaded changes nothing.</summary>
    public void Save()
    {
        if (Root is null)
            return;
        Sweep.DisabledPrefixes.Clear();
        foreach (var path in Root.UncheckedPrefixes())
            Sweep.DisabledPrefixes.Add(path);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(params string[] names)
    {
        foreach (var name in names)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// One folder row in the effect picker. Checked means the sweep rewrites what is inside, so the
/// tree starts fully checked apart from the paths the patch protects.
/// </summary>
public class EffectFolderViewModel : INotifyPropertyChanged
{
    private bool? _isChecked = true;
    private bool _isExpanded;
    private bool _isVisible = true;

    public EffectFolderViewModel(EffectTargets.Folder folder, EffectFolderViewModel? parent)
    {
        Path = folder.Path;
        Name = folder.Name;
        FileCount = folder.FileCount;
        Parent = parent;
        Children = [.. folder.Children.Select(c => new EffectFolderViewModel(c, this))];
        FolderCount = Children.Count + Children.Sum(c => c.FolderCount);
    }

    public string Path { get; }

    public string Name { get; }

    public int FileCount { get; }

    public EffectFolderViewModel? Parent { get; }

    public List<EffectFolderViewModel> Children { get; }

    /// <summary>Folders below this one, counted once so a row's label costs nothing to render.</summary>
    public int FolderCount { get; }

    public string Label => FolderCount == 0
        ? $"{Name}  ({FileCount})"
        : $"{Name}  ({FileCount} in {FolderCount} folders)";

    /// <summary>Null when some children are checked and some are not.</summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            OnPropertyChanged();
            if (value is bool decided)
                foreach (var child in Children)
                    child.IsChecked = decided;
            Parent?.Reconcile();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Hidden rows are the ones a search filtered out.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    private void Reconcile()
    {
        var state = Children.All(c => c.IsChecked == true) ? true
            : Children.All(c => c.IsChecked == false) ? false
            : (bool?)null;
        if (_isChecked == state)
            return;
        _isChecked = state;
        OnPropertyChanged(nameof(IsChecked));
        Parent?.Reconcile();
    }

    public IEnumerable<EffectFolderViewModel> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var deeper in child.Descendants())
                yield return deeper;
        }
    }

    /// <summary>Unchecks the folders a saved list, or the patch's own protect list, names.</summary>
    public void Uncheck(IReadOnlyCollection<string> prefixes)
    {
        if (prefixes.Any(p => Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            IsChecked = false;
            return;
        }
        foreach (var child in Children)
            child.Uncheck(prefixes);
    }

    /// <summary>
    /// The shortest set of paths that describes what is switched off, so a folder added by a
    /// later league inherits its parent's state instead of arriving already excluded.
    /// </summary>
    public IEnumerable<string> UncheckedPrefixes()
    {
        if (IsChecked == false)
        {
            yield return Path;
            yield break;
        }
        foreach (var child in Children)
            foreach (var path in child.UncheckedPrefixes())
                yield return path;
    }

    /// <summary>
    /// Shows rows matching the needle plus their parents, and expands to reveal them. Paths come
    /// from the monster name map, which knows folders whose own name carries nothing of the family.
    /// </summary>
    public bool Filter(string needle, IReadOnlyCollection<string>? paths = null)
    {
        var self = needle.Length == 0
            || Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || (paths?.Any(p => Path.Equals(p, StringComparison.OrdinalIgnoreCase)) ?? false);
        var any = false;
        foreach (var child in Children)
            any |= child.Filter(self ? string.Empty : needle, self ? null : paths);

        IsVisible = self || any;
        IsExpanded = needle.Length > 0 && any && !self;
        return IsVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
