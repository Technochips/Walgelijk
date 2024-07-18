﻿using Newtonsoft.Json;

namespace Walgelijk.AssetManager;

/// <summary>
/// Locally unique ID for an asset
/// </summary>
[JsonConverter(typeof(PackageIdConverter))]
public readonly struct PackageId : IEquatable<PackageId>
{
    /// <summary>
    /// Id of the asset package
    /// </summary>
    public readonly int External;

    public PackageId(string name)
    {
        External = IdUtil.Hash(name);
    }

    public PackageId(ReadOnlySpan<char> name)
    {
        External = IdUtil.Hash(name);
    }

    public PackageId(int numerical)
    {
        External = numerical;
    }

    /// <summary>
    /// Parses a formatted string. This function takes a stringified ID, <b>not a path</b>.
    /// </summary>
    public static PackageId Parse(ReadOnlySpan<char> id)
    {
        if (TryParse(id, out var asset))
            return asset;

        throw new Exception($"{id} is not a valid ID");
    }

    /// <summary>
    /// Parses a formatted string. This function takes a stringified ID, <b>not a path</b>.
    /// </summary>
    public static PackageId Parse(string id)
    {
        return Parse(id.AsSpan());
    }

    /// <summary>
    /// Parses a formatted string. This function takes a stringified ID, <b>not a path</b>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> id, out PackageId asset)
    {
        if (IdUtil.TryConvert(id, out var i))
        {
            asset = new PackageId(i);
            return true;
        }

        asset = default;
        return false;
    }

    public override string ToString() => IdUtil.Convert(External);

    public override bool Equals(object? obj)
    {
        return obj is AssetId id && Equals(id);
    }

    public bool Equals(PackageId other)
    {
        return External == other.External;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(External);
    }

    public static readonly PackageId None = default;

    public static bool operator ==(PackageId left, PackageId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PackageId left, PackageId right)
    {
        return !(left == right);
    }
}
