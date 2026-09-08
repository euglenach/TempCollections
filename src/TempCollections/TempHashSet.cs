namespace TempCollections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides a stack-only, pooled hash set for short-lived, unique data.
/// </summary>
/// <remarks>
/// Dispose the set to return its rented arrays to the pool. This type is not thread-safe.
/// </remarks>
[CollectionBuilder(typeof(TempHashSet), nameof(TempHashSet.Create))]
public ref struct TempHashSet<T>
{
    private Span<int> buckets;
    private Span<Entry> entries;
    private int[]? pooledBuckets;
    private Entry[]? pooledEntries;
    private IEqualityComparer<T>? comparer;
    private int count;
    private int freeCount;
    private int freeList;

    /// <summary>
    /// Initializes an empty set with the requested initial capacity.
    /// </summary>
    public TempHashSet(int capacity)
        : this(capacity, null)
    {
    }

    /// <summary>
    /// Initializes an empty set with the requested initial capacity and equality comparer.
    /// </summary>
    public TempHashSet(int capacity, IEqualityComparer<T>? comparer)
    {
        if(capacity < 0)
        {
            ThrowArgumentOutOfRangeException(nameof(capacity));
        }

        buckets = default;
        entries = default;
        pooledBuckets = null;
        pooledEntries = null;
        this.comparer = comparer;
        count = 0;
        freeCount = 0;
        freeList = -1;

        if(capacity > 0)
        {
            Resize(capacity);
        }
    }

    /// <summary>
    /// Gets the number of items currently stored in the set.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => count - freeCount;
    }

    /// <summary>
    /// Gets the equality comparer used to compare items.
    /// </summary>
    public IEqualityComparer<T> Comparer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>
    /// Adds an item to the set.
    /// </summary>
    /// <returns><see langword="true"/> when the item was added; otherwise, <see langword="false"/>.</returns>
    public bool Add(T item)
    {
        var equalityComparer = Comparer;
        var hashCode = GetItemHashCode(item, equalityComparer);

        if(buckets.IsEmpty)
        {
            Resize(4);
        }

        var bucketIndex = GetBucketIndex(hashCode);
        for(var entryIndex = buckets[bucketIndex] - 1; entryIndex >= 0; entryIndex = entries[entryIndex].Next)
        {
            ref var entry = ref entries[entryIndex];
            if(entry.HashCode == hashCode && equalityComparer.Equals(entry.Value, item))
            {
                return false;
            }
        }

        int index;
        if(freeCount > 0)
        {
            index = freeList;
            freeList = entries[index].Next;
            freeCount--;
        }
        else
        {
            if(count == entries.Length)
            {
                ResizeForOneMoreItem();
                bucketIndex = GetBucketIndex(hashCode);
            }

            index = count;
            count++;
        }

        entries[index] = new Entry
        {
            HashCode = hashCode,
            Next = buckets[bucketIndex] - 1,
            Value = item,
        };
        buckets[bucketIndex] = index + 1;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified item.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => FindItemIndex(item) >= 0;

    /// <summary>
    /// Removes the specified item from the set.
    /// </summary>
    /// <returns><see langword="true"/> when the item was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(T item)
    {
        if(buckets.IsEmpty)
        {
            return false;
        }

        var equalityComparer = Comparer;
        var hashCode = GetItemHashCode(item, equalityComparer);
        var bucketIndex = GetBucketIndex(hashCode);
        var previousIndex = -1;

        for(var entryIndex = buckets[bucketIndex] - 1; entryIndex >= 0; previousIndex = entryIndex, entryIndex = entries[entryIndex].Next)
        {
            ref var entry = ref entries[entryIndex];
            if(entry.HashCode != hashCode || !equalityComparer.Equals(entry.Value, item))
            {
                continue;
            }

            if(previousIndex < 0)
            {
                buckets[bucketIndex] = entry.Next + 1;
            }
            else
            {
                entries[previousIndex].Next = entry.Next;
            }

            entry = new Entry
            {
                HashCode = -1,
                Next = freeList,
            };
            freeList = entryIndex;
            freeCount++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all items from the set.
    /// </summary>
    public void Clear()
    {
        if(count == 0)
        {
            return;
        }

        buckets.Clear();
        if(RuntimeHelpers.IsReferenceOrContainsReferences<Entry>())
        {
            entries[..count].Clear();
        }

        count = 0;
        freeCount = 0;
        freeList = -1;
    }

    /// <summary>
    /// Returns the rented backing arrays to the shared array pool.
    /// </summary>
    public void Dispose()
    {
        var oldBuckets = pooledBuckets;
        var oldEntries = pooledEntries;

        buckets = default;
        entries = default;
        pooledBuckets = null;
        pooledEntries = null;
        comparer = null;
        count = 0;
        freeCount = 0;
        freeList = -1;

        if(oldBuckets is not null)
        {
            ArrayPool<int>.Shared.Return(oldBuckets);
        }

        if(oldEntries is not null)
        {
            ArrayPool<Entry>.Shared.Return(oldEntries, RuntimeHelpers.IsReferenceOrContainsReferences<Entry>());
        }
    }

    /// <summary>
    /// Returns an enumerator over the items currently stored in the set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(entries, count);

    private int FindItemIndex(T item)
    {
        if(buckets.IsEmpty)
        {
            return -1;
        }

        var equalityComparer = Comparer;
        var hashCode = GetItemHashCode(item, equalityComparer);
        var bucketIndex = GetBucketIndex(hashCode);

        for(var entryIndex = buckets[bucketIndex] - 1; entryIndex >= 0; entryIndex = entries[entryIndex].Next)
        {
            ref var entry = ref entries[entryIndex];
            if(entry.HashCode == hashCode && equalityComparer.Equals(entry.Value, item))
            {
                return entryIndex;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int hashCode) => hashCode & (buckets.Length - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetItemHashCode(T item, IEqualityComparer<T> comparer) => comparer.GetHashCode(item!) & int.MaxValue;

    private void ResizeForOneMoreItem()
    {
        var oldCapacity = entries.Length;
        if(oldCapacity >= 1 << 30)
        {
            ThrowOutOfMemoryException();
        }

        Resize(oldCapacity * 2);
    }

    private void Resize(int minimumCapacity)
    {
        var newCapacity = GetCapacity(minimumCapacity);
        var newBuckets = ArrayPool<int>.Shared.Rent(newCapacity);
        var newEntries = ArrayPool<Entry>.Shared.Rent(newCapacity);
        var newBucketSpan = newBuckets.AsSpan(0, newCapacity);
        var newEntrySpan = newEntries.AsSpan(0, newCapacity);
        newBucketSpan.Clear();

        entries[..count].CopyTo(newEntrySpan);
        for(var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            ref var entry = ref newEntrySpan[entryIndex];
            if(entry.HashCode < 0)
            {
                continue;
            }

            var bucketIndex = entry.HashCode & (newCapacity - 1);
            entry.Next = newBucketSpan[bucketIndex] - 1;
            newBucketSpan[bucketIndex] = entryIndex + 1;
        }

        var oldBuckets = pooledBuckets;
        var oldEntries = pooledEntries;
        buckets = newBucketSpan;
        entries = newEntrySpan;
        pooledBuckets = newBuckets;
        pooledEntries = newEntries;

        if(oldBuckets is not null)
        {
            ArrayPool<int>.Shared.Return(oldBuckets);
        }

        if(oldEntries is not null)
        {
            ArrayPool<Entry>.Shared.Return(oldEntries, RuntimeHelpers.IsReferenceOrContainsReferences<Entry>());
        }
    }

    private static int GetCapacity(int minimumCapacity)
    {
        if(minimumCapacity > 1 << 30)
        {
            ThrowOutOfMemoryException();
        }

        var capacity = Math.Max(minimumCapacity, 4) - 1;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return capacity + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfMemoryException() => throw new OutOfMemoryException();

    internal struct Entry
    {
        public int HashCode;
        public int Next;
        public T Value;
    }

    /// <summary>
    /// Enumerates the items in a <see cref="TempHashSet{T}"/>.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly Span<Entry> entries;
        private readonly int count;
        private int index;

        internal Enumerator(Span<Entry> entries, int count)
        {
            this.entries = entries;
            this.count = count;
            index = -1;
        }

        /// <summary>
        /// Gets the current item.
        /// </summary>
        public T Current => entries[index].Value;

        /// <summary>
        /// Advances the enumerator to the next item.
        /// </summary>
        public bool MoveNext()
        {
            while(++index < count)
            {
                if(entries[index].HashCode >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// Provides factory methods for <see cref="TempHashSet{T}"/>.
/// </summary>
public static class TempHashSet
{
    /// <summary>
    /// Creates a set that contains the unique items from the specified span.
    /// </summary>
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> items)
    {
        var set = new TempHashSet<T>(items.Length);
        foreach(var item in items)
        {
            set.Add(item);
        }

        return set;
    }
}
