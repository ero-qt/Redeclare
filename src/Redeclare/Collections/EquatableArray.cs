using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Redeclare;

/// <summary>
///     An immutable array compared by its contents, so a record holding one compares by value and an
///     incremental generator's cache can hit on it.
/// </summary>
/// <remarks>
///     A default instance is an empty array. The wrapped array is never exposed, so no caller can
///     mutate it after construction. Collection expressions build one: <c>[a, b, c]</c>.
/// </remarks>
[CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly T[]? _items;

    /// <summary>
    ///     Wraps a copy of <paramref name="items"/>, or shares the array of another <see cref="EquatableArray{T}"/>.
    /// </summary>
    public EquatableArray(IEnumerable<T> items)
    {
        _items = items switch
        {
            null => null,
            EquatableArray<T> other => other._items,
            T[] array => array.Length == 0 ? null : (T[])array.Clone(),
            ICollection<T> { Count: 0 } => null,
            _ => ToArray(items) is { Length: > 0 } copied ? copied : null,
        };
    }

    private EquatableArray(T[]? owned)
    {
        _items = owned is { Length: 0 } ? null : owned;
    }

    /// <summary>
    ///     Gets the empty array.
    /// </summary>
    public static EquatableArray<T> Empty => default;

    /// <summary>
    ///     Gets the number of elements.
    /// </summary>
    public int Length => _items?.Length ?? 0;

    /// <inheritdoc/>
    public int Count => Length;

    /// <summary>
    ///     Gets whether there are no elements.
    /// </summary>
    public bool IsEmpty => _items is null;

    /// <inheritdoc/>
    public T this[int index]
    {
        get
        {
            if (_items is null)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The array is empty.");
            }

            return _items[index];
        }
    }

    /// <summary>
    ///     Wraps <paramref name="items"/> without copying; the caller gives up the array.
    /// </summary>
    internal static EquatableArray<T> Own(T[]? items)
    {
        // Wraps the array as is. A collection expression would copy it.
#pragma warning disable IDE0028
        return new(items);
#pragma warning restore IDE0028
    }

    /// <summary>
    ///     The elements as a span, without copying.
    /// </summary>
    public ReadOnlySpan<T> AsSpan()
    {
        return _items is null ? default : _items.AsSpan();
    }

    /// <summary>
    ///     Copies the elements into a new array.
    /// </summary>
    public T[] ToArray()
    {
        return _items is null ? [] : (T[])_items.Clone();
    }

    /// <summary>
    ///     Returns an array with <paramref name="item"/> appended.
    /// </summary>
    public EquatableArray<T> Add(T item)
    {
        var result = new T[Length + 1];
        _items?.CopyTo(result, 0);
        result[Length] = item;

        return Own(result);
    }

    /// <summary>
    ///     Returns an array with <paramref name="items"/> appended.
    /// </summary>
    public EquatableArray<T> AddRange(IEnumerable<T> items)
    {
        var added = ToArray(items);
        if (added.Length == 0)
        {
            return this;
        }

        var result = new T[Length + added.Length];
        _items?.CopyTo(result, 0);
        added.CopyTo(result, Length);

        return Own(result);
    }

    /// <inheritdoc/>
    public bool Equals(EquatableArray<T> other)
    {
        if (ReferenceEquals(_items, other._items))
        {
            return true;
        }

        if (_items is null || other._items is null || _items.Length != other._items.Length)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < _items.Length; i++)
        {
            if (!comparer.Equals(_items[i], other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (_items is null)
        {
            return 0;
        }

        var comparer = EqualityComparer<T>.Default;

        // Mixes the hashes FNV-style. netstandard2.0 has no `System.HashCode`.
        unchecked
        {
            int hash = (int)2166136261;
            foreach (var item in _items)
            {
                hash = (hash ^ (item is null ? 0 : comparer.GetHashCode(item))) * 16777619;
            }

            return hash;
        }
    }

    /// <summary>
    ///     Compares two arrays by contents.
    /// </summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two arrays by contents.
    /// </summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    ///     Wraps a copy of an array.
    /// </summary>
    public static implicit operator EquatableArray<T>(T[] items)
    {
        return items is null or { Length: 0 } ? default : Own((T[])items.Clone());
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? [])).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"EquatableArray<{typeof(T).Name}>[{Length}]";
    }

    private static T[] ToArray(IEnumerable<T> items)
    {
        return items switch
        {
            null => [],
            T[] array => array.Length == 0 ? [] : (T[])array.Clone(),
            ICollection<T> collection => CopyOf(collection),
            _ => [.. items],
        };
    }

    private static T[] CopyOf(ICollection<T> collection)
    {
        var result = new T[collection.Count];
        collection.CopyTo(result, 0);

        return result;
    }
}

/// <summary>
///     Builders and conversions for <see cref="EquatableArray{T}"/>.
/// </summary>
internal static class EquatableArray
{
    /// <summary>
    ///     The collection-expression builder: <c>EquatableArray&lt;int&gt; xs = [1, 2];</c>.
    /// </summary>
    public static EquatableArray<T> Create<T>(ReadOnlySpan<T> items)
    {
        return items.IsEmpty ? default : EquatableArray<T>.Own(items.ToArray());
    }

    /// <summary>
    ///     Copies a sequence into an <see cref="EquatableArray{T}"/>, once.
    /// </summary>
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items)
    {
        // Copies the items once, through `CopyTo`. A collection expression would copy them twice.
#pragma warning disable IDE0028, IDE0306
        return new(items);
#pragma warning restore IDE0028, IDE0306
    }
}
