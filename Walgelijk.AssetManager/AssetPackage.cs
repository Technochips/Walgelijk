﻿using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Walgelijk.AssetManager.Deserialisers;

namespace Walgelijk.AssetManager;

public class AssetPackage : IDisposable
{
    public readonly AssetPackageMetadata Metadata;
    public readonly IReadArchive Archive;
    public readonly ImmutableHashSet<AssetId> All = [];

    private readonly ImmutableDictionary<int, string> guidTable;
    private readonly AssetFolder hierarchyRoot = new("/");

    private readonly ConcurrentDictionary<AssetId, object> cache = [];
    private readonly ConcurrentDictionary<string, AssetId[]> taggedCache = [];
    private readonly ConcurrentDictionary<AssetId, AssetMetadata> metadataCache = [];

    private readonly ReaderWriterLockSlim assetReadingLock = new(LockRecursionPolicy.SupportsRecursion);

    private bool disposed;
    private bool isDeserialising;

    public AssetPackage(Stream input)
    {
        assetReadingLock.EnterWriteLock();

        var guidTable = new Dictionary<int, string>();
        var archive = new TarReadArchive(input);
        var all = new HashSet<AssetId>();
        Archive = archive;
        // read metadata
        {
            var metadataEntry = archive.GetEntry("package.json") ?? throw new Exception("Archive has no package.json. This asset package is invalid");
            using var e = new StreamReader(metadataEntry!, encoding: Encoding.UTF8, leaveOpen: false);
            Metadata = JsonConvert.DeserializeObject<AssetPackageMetadata>(e.ReadToEnd());
        }

        // read guid table
        {
            var guidTableEntry = archive.GetEntry("guid_table.txt") ?? throw new Exception("Archive has no guid_table.txt. This asset package is invalid.");
            using var e = new StreamReader(guidTableEntry!, encoding: Encoding.UTF8, leaveOpen: false);

            while (true)
            {
                var idLine = e.ReadLine();
                if (idLine == null)
                    break;

                if (!int.TryParse(idLine, out var id))
                    throw new Exception($"Id {idLine} is not an integer");

                var path = e.ReadLine() ?? throw new Exception($"Invalid GUID table: id {idLine} is missing path");

                AssetPackageUtils.AssertPathValidity(path);

                guidTable.Add(id, path);
            }

            this.guidTable = guidTable.ToImmutableDictionary();
        }

        // read hierarchy
        {
            var hierarchyEntry = archive.GetEntry("hierarchy.txt") ?? throw new Exception("Archive has no hierarchy.txt. This asset package is invalid.");
            using var e = new StreamReader(hierarchyEntry!, encoding: Encoding.UTF8, leaveOpen: false);

            while (true)
            {
                // TODO error handling

                var path = e.ReadLine();

                if (path == null)
                    break;

                var assetFolder = EnsureHierarchyFolder(path);
                var count = int.Parse(e.ReadLine()!);
                assetFolder.Assets = new AssetId[count];
                for (int i = 0; i < count; i++)
                {
                    int asset = int.Parse(e.ReadLine()!);
                    var b = assetFolder.Assets[i] = new(asset);
                    all.Add(b);
                }
            }
        }

        All = [.. all];

        // read tagged cache
        {
            var tagTableEntry = archive.GetEntry("tag_table.txt") ?? throw new Exception("Archive has no tag_table.txt. This asset package is invalid.");
            using var e = new StreamReader(tagTableEntry!, encoding: Encoding.UTF8, leaveOpen: false);

            while (true)
            {
                // TODO error handling

                var tag = e.ReadLine();

                if (tag == null)
                    break;

                var count = int.Parse(e.ReadLine()!);
                var arr = new AssetId[count];
                taggedCache.AddOrSet(tag, arr);

                for (int i = 0; i < count; i++)
                {
                    int asset = int.Parse(e.ReadLine()!);
                    arr[i] = new AssetId(asset);
                }
            }
        }

        assetReadingLock.ExitWriteLock();
    }

    private static IEnumerable<AssetId> EnumerateFolder(AssetFolder folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (folder.Assets != null)
            foreach (var asset in folder.Assets)
                yield return asset;

        if (searchOption == SearchOption.AllDirectories && folder.Folders != null)
            foreach (var c in folder.Folders)
                foreach (var item in EnumerateFolder(c, searchOption))
                    yield return item;
    }

    public IEnumerable<AssetId> EnumerateFolder(ReadOnlySpan<char> path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (TryGetAssetFolder(path, out var folder))
            return EnumerateFolder(folder, searchOption);
        return [];
    }

    public IEnumerable<AssetId> QueryTags(in string tag)
    {
        if (taggedCache.TryGetValue(tag, out var assets))
            return assets;
        return [];
    }


    public Asset GetAsset(in AssetId id)
    {
        if (id.Internal == 0)
            throw new Exception("Id is None");

        var path = GetAssetPath(id);
        var p = "assets/" + path;// TODO we should cache this string concatenation bullshit somewhere

        if (!Archive.HasEntry(p))
            throw new Exception($"Asset {id.Internal} at path \"{path}\" not found. This indicates the archive is malformed.");
        // it is malformed because the guid table is pointing to entries that don't exist

        // TODO we are doing unnecessary work by calling this function (double GetAssetPath)
        return new Asset(GetAssetMetadata(id), () => Archive.GetEntry(p)!);
    }

    public string GetAssetPath(in AssetId id)
    {
        if (guidTable.TryGetValue(id.Internal, out var path))
            return path;

        throw new Exception($"Asset {id.Internal} does not exist.");
    }

    public bool HasAsset(in AssetId id) => guidTable.ContainsKey(id.Internal);

    public T Load<T>(in string path) => Load<T>(new AssetId(path));

    public T Load<T>(in AssetId id)
    {
        if (id.Internal == 0)
            throw new Exception("Id is None");

        assetReadingLock.EnterUpgradeableReadLock();

        var metadata = GetAssetMetadata(id);

        try
        {
            if (cache.TryGetValue(id, out var obj))
            {
                if (obj is T t)
                    return t;
                else
                    throw new Exception($"Asset {id.Internal} was previously loaded as type {obj.GetType()}, this does not match the requested type {typeof(T)}");
            }
            if (isDeserialising)
                throw new Exception("Loading unloaded assets during deserialisation is not allowed");
            assetReadingLock.EnterWriteLock();
            isDeserialising = true;
            try
            {
                var a = AssetDeserialisers.Load<T>(GetAsset(id)) ?? throw new NullReferenceException($"Deserialising asset {id.Internal} returns null");
                if (!cache.TryAdd(id, a))
                    throw new Exception("");
                return a;
            }
            finally
            {
                isDeserialising = false;
                assetReadingLock.ExitWriteLock();
            }
        }
        finally
        {
            assetReadingLock.ExitUpgradeableReadLock();
        }
    }

    public T LoadNoCache<T>(AssetId id)
    {
        if (id.Internal == 0)
            throw new Exception("Id is None");

        assetReadingLock.EnterWriteLock();

        try
        {
            var a = AssetDeserialisers.Load<T>(GetAsset(id))
                ?? throw new NullReferenceException($"Deserialising asset {id.Internal} returns null");
            return a;
        }
        finally
        {
            assetReadingLock.ExitWriteLock();
        }
    }

    public void DisposeOf(in AssetId id)
    {
        assetReadingLock.EnterWriteLock();

        try
        {
            if (cache.TryRemove(id, out var obj))
                if (obj is IDisposable v)
                    v.Dispose();
        }
        finally
        {
            assetReadingLock.ExitWriteLock();
        }
    }

    public bool IsCached(in AssetId id) => cache.ContainsKey(id);

    public bool TryGetCached(in AssetId id, [NotNullWhen(true)] out object? value) => cache.TryGetValue(id, out value);

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (var v in cache.Values)
        {
            if (v is IDisposable a)
                a.Dispose();
            else if (v is IAsyncDisposable b)
                Task.Run(b.DisposeAsync).RunSynchronously();
        }

        cache.Clear();
        Archive.Dispose();
        assetReadingLock.Dispose();
        disposed = true;
    }

    public static AssetPackage Load(string path)
    {
        var file = new FileStream(path, FileMode.Open);
        return new AssetPackage(file);
    }

    public AssetMetadata GetAssetMetadata(in AssetId id)
    {
        if (metadataCache.TryGetValue(id, out var value))
            return value;

        var path = GetAssetPath(id);
        var p = "metadata/" + path + ".json";// TODO cache string concat
        if (!Archive.HasEntry(p))
            throw new Exception($"Asset metadata {id.Internal} at path \"{path}\" not found. This indicates the archive is malformed.");
        // it is malformed because the guid table is pointing to entries that don't exist

        using var reader = new StreamReader(Archive.GetEntry(p)!, encoding: Encoding.UTF8, leaveOpen: false);
        var json = reader.ReadToEnd();
        value = JsonConvert.DeserializeObject<AssetMetadata>(json);
        metadataCache.AddOrSet(id, value);
        return value;
    }

    private bool TryGetAssetFolder(ReadOnlySpan<char> path, [NotNullWhen(true)] out AssetFolder? folder)
    {
        if (path.IsEmpty || path == "/")
        {
            folder = hierarchyRoot;
            return true;
        }

        folder = null;
        var navigator = hierarchyRoot;
        ReadOnlySpan<char> current = path;
        while (true)
        {
            current = EatFolder(current, out var eaten);

            if (navigator.TryGetFolder(eaten, out var found))
                navigator = found;
            else
                return false;

            if (current.IsEmpty)
                break;
        }

        folder = navigator;
        return true;

        static ReadOnlySpan<char> EatFolder(ReadOnlySpan<char> input, out ReadOnlySpan<char> folder)
        {
            int i = input.IndexOf('/');
            if (i == -1)
            {
                folder = input;
                return [];
            }

            folder = input[..i];
            return input[(i + 1)..];
        }
    }

    private AssetFolder EnsureHierarchyFolder(ReadOnlySpan<char> path)
    {
        if (path.Length > 0 && path[^1] == '/')
            path = path[..^1];

        if (TryGetAssetFolder(path, out var folder))
            return folder;

        var l = path.LastIndexOf('/');

        AssetFolder? parent = null;

        if (l == -1)
            parent = hierarchyRoot;
        else
            parent = EnsureHierarchyFolder(path[..l]);

        var created = new AssetFolder(path[(l + 1)..].ToString());
        if (parent.Folders == null)
            parent.Folders = [created];
        else
            parent.Folders = [.. parent.Folders.Append(created)];
        return created;
    }

    private class AssetFolder
    {
        /// <summary>
        /// The name of this folder. NOT THE PATH!
        /// </summary>
        public readonly string Name;

        public AssetFolder(string name)
        {
            Name = name;
        }

        public AssetFolder[]? Folders;
        public AssetId[]? Assets;

        public bool TryGetFolder(ReadOnlySpan<char> name, [NotNullWhen(true)] out AssetFolder? f)
        {
            f = null;

            if (Folders == null)
                return false;

            foreach (var i in Folders)
                if (name.SequenceEqual(i.Name))
                {
                    f = i;
                    return true;
                }

            return false;
        }
    }
}
