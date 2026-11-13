using LibBundle3.Nodes;

namespace PoeRedux.Patches;

/// <summary>
/// Lookups in the game's file tree. Paths are relative to the root, separated by '/', and names match exactly.
/// </summary>
internal static class GameTree
{
    public static DirectoryNode? FindDirectory(DirectoryNode root, string path)
    {
        var current = root;
        foreach (var name in path.Split('/'))
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null)
                return null;
            current = next;
        }
        return current;
    }

    public static FileNode? FindFile(DirectoryNode root, string path)
    {
        var slash = path.LastIndexOf('/');
        var dir = slash < 0 ? root : FindDirectory(root, path[..slash]);
        var name = path[(slash + 1)..];
        return dir?.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == name);
    }

    /// <summary>
    /// Files in <paramref name="dir"/> and all its subdirectories whose names end with one of
    /// <paramref name="extensions"/> (any case), in tree order. Empty when <paramref name="dir"/> is null.
    /// </summary>
    public static List<FileNode> FindFiles(DirectoryNode? dir, params string[] extensions)
    {
        var files = new List<FileNode>();
        if (dir is not null)
            AddFiles(dir, extensions, files);
        return files;
    }

    private static void AddFiles(DirectoryNode dir, string[] extensions, List<FileNode> files)
    {
        foreach (var node in dir.Children)
        {
            if (node is DirectoryNode child)
                AddFiles(child, extensions, files);
            else if (node is FileNode file && extensions.Any(ext => file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                files.Add(file);
        }
    }
}
