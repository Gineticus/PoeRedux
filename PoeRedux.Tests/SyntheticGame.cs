using LibBundle3;
using LibBundle3.Records;
using System.Text;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace PoeRedux.Tests;

/// <summary>
/// A Steam-style install (Bundles2/_.index.bin plus bundles) written in the format LibBundle3 reads, in a
/// temporary folder, with backups and settings redirected next to it.
/// </summary>
public sealed class SyntheticGame : IDisposable
{
    private readonly string _root;

    public SyntheticGame(IReadOnlyDictionary<string, byte[]> files, int maxBundleSize = 64 * 1024)
    {
        _root = Path.Combine(Path.GetTempPath(), "poeredux-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Write(Path.Combine(_root, "game"), files, maxBundleSize);
        Services.BackupStore.BackupDirectory = Path.Combine(_root, "backups");
    }

    public string IndexPath => Path.Combine(_root, "game", "Bundles2", "_.index.bin");
    public string BackupDirectory => Path.Combine(_root, "backups");

    /// <summary>Runs <paramref name="action"/> on the game index; with <paramref name="parsePaths"/> false, as Restore opens it.</summary>
    public T Use<T>(Func<LibBundle3.Index, T> action, bool parsePaths = true)
    {
        using var index = new LibBundle3.Index(IndexPath, false);
        if (parsePaths)
            index.ParsePaths();
        return action(index);
    }

    public Dictionary<string, byte[]> ReadAll() => Use(index =>
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        LibBundle3.Index.Extract(index.Files.Values, (record, content) =>
        {
            result[record.Path] = content?.ToArray() ?? throw new InvalidDataException("missing bundle for " + record.Path);
            return false;
        });
        return result;
    });

    /// <summary>Replaces files the way a game update does: new bytes, written outside PoE Redux.</summary>
    public void Update(IReadOnlyDictionary<string, byte[]> files) => Use(index =>
    {
        foreach (var (path, bytes) in files)
        {
            index.TryGetFile(path, out var record);
            record!.Write(bytes);
        }
        index.Save();
        return 0;
    });

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    public static byte[] Utf16(string text) => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];

    public static string Text(byte[] bytes) => Encoding.Unicode.GetString(bytes).TrimStart('﻿');

    private static void Write(string gameDir, IReadOnlyDictionary<string, byte[]> files, int maxBundleSize)
    {
        var bundles2 = Path.Combine(gameDir, "Bundles2");
        Directory.CreateDirectory(bundles2);

        var names = new List<string>();
        var sizes = new List<int>();
        var records = new List<(ulong Hash, int Bundle, int Offset, int Size)>();
        var current = new MemoryStream();

        void Flush()
        {
            var name = $"Folders/test/bundle{names.Count}";
            var file = Path.Combine(bundles2, name + ".bundle.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, TestBundle.Build(current.GetBuffer().AsSpan(0, (int)current.Length)));
            names.Add(name);
            sizes.Add((int)current.Length);
            current.SetLength(0);
        }

        foreach (var (path, data) in files)
        {
            if (current.Length > 0 && current.Length + data.Length > maxBundleSize)
                Flush();
            records.Add((HashHelper.PathHash(path), names.Count, (int)current.Length, data.Length));
            current.Write(data);
        }
        Flush();

        var directory = new MemoryStream();
        foreach (var path in files.Keys)
        {
            directory.Write(BitConverter.GetBytes(1));
            directory.Write(Encoding.UTF8.GetBytes(path));
            directory.WriteByte(0);
        }
        var directoryBundle = TestBundle.Build(directory.ToArray());

        var index = new MemoryStream();
        var w = new BinaryWriter(index);
        w.Write(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            var n = Encoding.ASCII.GetBytes(names[i]);
            w.Write(n.Length);
            w.Write(n);
            w.Write(sizes[i]);
        }
        w.Write(records.Count);
        foreach (var r in records)
        {
            w.Write(r.Hash);
            w.Write(r.Bundle);
            w.Write(r.Offset);
            w.Write(r.Size);
        }
        w.Write(1); // one directory record covering every path
        w.Write(0xF42A94E69CFF42FEul); // hash of "": MurmurHash names, as in PoE since 3.21.2
        w.Write(0);
        w.Write((int)directory.Length);
        w.Write((int)directory.Length);
        w.Write(directoryBundle);
        w.Flush();
        File.WriteAllBytes(Path.Combine(bundles2, "_.index.bin"), TestBundle.Build(index.ToArray()));
    }

    private sealed class TestBundle(Stream stream) : Bundle(stream, (BundleRecord?)null)
    {
        public static byte[] Build(ReadOnlySpan<byte> content)
        {
            var ms = new MemoryStream();
            using (var bundle = new TestBundle(ms))
                bundle.Save(content);
            return ms.ToArray();
        }
    }

    private sealed class HashHelper : LibBundle3.Index
    {
        private HashHelper() : base((Stream)null!) { }
        public static ulong PathHash(string path) => MurmurHash64A(Encoding.UTF8.GetBytes(path));
    }
}
