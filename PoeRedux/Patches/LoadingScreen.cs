using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class LoadingScreen : IPatch
{
    public string Name => "Loading Screen Patch";
    public object Description => "Hides the loading screen artwork instantly instead of fading in.";

    private List<FileNode> fileNodes = [];

    private bool TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return false;

        var lines = data.Split("\r\n").ToList();
        var changed = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith("begin", StringComparison.Ordinal))
                continue;
            if (i + 1 < lines.Count && lines[i + 1].Contains("mouse_enabled = false"))
                continue;

            lines.Insert(i + 1, "\tmouse_enabled = false;");
            lines.Insert(i + 1, "\tvisible = false;");
            changed = true;
        }

        if (!changed)
            return false;

        var newBytes = Encoding.Unicode.GetBytes(string.Join("\r\n", lines));
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    public void Apply(DirectoryNode root)
    {
        var dir = NavigateTo(root, "metadata", "ui", "loadingstate");
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }

        fileNodes = dir.Children.OfType<FileNode>()
            .Where(f => f.Name.EndsWith(".ui", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fileNodes.Count == 0)
        {
            PatchReport.NoMatch(Scope, ".ui files");
            return;
        }

        var changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;

        if (changed == 0)
            PatchReport.NoMatch(Scope, "a line starting with \"begin\"");
    }

    private const string Scope = "metadata/ui/loadingstate";
}
