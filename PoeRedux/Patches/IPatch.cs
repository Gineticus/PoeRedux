namespace PoeRedux.Patches;

public interface IPatch {
    string Name { get; }
    object Description { get; }
    void Apply(LibBundle3.Nodes.DirectoryNode root);
}