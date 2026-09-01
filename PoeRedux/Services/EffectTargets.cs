using BundleIndex = LibBundle3.Index;

namespace PoeRedux.Services;

/// <summary>
/// The folder tree a selective sweep can be pointed at, counted from the game files rather than
/// from a list kept in the tool, so a league that adds effects shows up without a code change.
/// </summary>
public static class EffectTargets
{
    /// <summary>What to scan: one patch's root, its label, and the extensions it rewrites.</summary>
    public sealed record Sweep(string Root, string Label, IReadOnlyList<string> Extensions);

    /// <summary>A folder that holds files the sweep would rewrite, and the folders under it.</summary>
    public sealed class Folder(string path, string name)
    {
        public string Path { get; } = path;

        /// <summary>Display name. A chain of single child folders is joined into one row.</summary>
        public string Name { get; set; } = name;

        /// <summary>Files the sweep would rewrite here and in everything below.</summary>
        public int FileCount { get; set; }

        public List<Folder> Children { get; } = [];
    }

    public sealed record Tree(string Root, string Label, Folder Folder);

    public static Task<List<Tree>> LoadAsync(string gameFilePath, IReadOnlyList<Sweep> sweeps) =>
        Task.Run(() => Load(gameFilePath, sweeps));

    public static List<Tree> Load(string gameFilePath, IReadOnlyList<Sweep> sweeps) =>
        GameFile.Read(gameFilePath, index => Scan(index, sweeps));

    public static List<Tree> Scan(BundleIndex index, IReadOnlyList<Sweep> sweeps)
    {
        var counts = sweeps.ToDictionary(s => s.Root, _ => new Dictionary<string, int>(StringComparer.Ordinal));

        foreach (var file in index.Files.Values)
        {
            var path = file.Path;
            foreach (var sweep in sweeps)
            {
                if (!path.StartsWith(sweep.Root, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!sweep.Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var slash = path.LastIndexOf('/');
                if (slash > 0)
                {
                    var folder = path[..slash];
                    var folders = counts[sweep.Root];
                    folders[folder] = folders.GetValueOrDefault(folder) + 1;
                }
                break;
            }
        }

        return [.. sweeps.Select(s => new Tree(s.Root, s.Label, new Builder(s.Root, counts[s.Root]).Build()))];
    }

    /// <summary>
    /// Grows the tree from the folders that hold files, then collapses away the links that only
    /// exist to hold one other folder.
    /// </summary>
    private sealed class Builder(string root, Dictionary<string, int> counts)
    {
        private readonly Dictionary<string, Folder> _byPath = new(StringComparer.Ordinal);

        /// <summary>Files in the folder itself, which is what decides whether a link can go.</summary>
        private readonly Dictionary<Folder, int> _own = [];

        public Folder Build()
        {
            var tree = new Folder(root, root);
            _byPath[root] = tree;

            foreach (var (folder, count) in counts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                _own[Reach(folder)] = count;

            Total(tree);
            Collapse(tree);
            return tree;
        }

        private Folder Reach(string folder)
        {
            if (_byPath.TryGetValue(folder, out var existing))
                return existing;

            var slash = folder.LastIndexOf('/');
            var parent = Reach(slash > root.Length ? folder[..slash] : root);

            var node = new Folder(folder, folder[(slash + 1)..]);
            parent.Children.Add(node);
            _byPath[folder] = node;
            return node;
        }

        private int Total(Folder folder)
        {
            folder.FileCount = _own.GetValueOrDefault(folder) + folder.Children.Sum(Total);
            return folder.FileCount;
        }

        /// <summary>
        /// A deep chain such as revive/mat/bu_explosion becomes one row instead of three clicks.
        /// </summary>
        private void Collapse(Folder folder)
        {
            foreach (var child in folder.Children)
                Collapse(child);

            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                while (child.Children.Count == 1 && _own.GetValueOrDefault(child) == 0)
                {
                    var only = child.Children[0];
                    only.Name = child.Name + "/" + only.Name;
                    folder.Children[i] = child = only;
                }
            }

            folder.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }
    }
}
