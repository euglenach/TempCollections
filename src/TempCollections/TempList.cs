namespace TempCollections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Provides a stack-only, pooled, growable list for short-lived data.
/// </summary>
/// <remarks>
/// Dispose the list to return its rented array to the pool. This type is not thread-safe.
/// </remarks>
[CollectionBuilder(typeof(TempList), nameof(TempList.Create))]
public ref struct TempList<T>
{
    private Span<T> buffer;
    private T[]? pooledArray;
    private int size;
    
    /// <summary>
    /// Initializes an empty list with the requested initial capacity.
    /// </summary>
    public TempList(int defaultCapacity)
    {
        if(defaultCapacity == 0)
        {
            pooledArray = null;
            buffer = default;
        }
        else
        {
            pooledArray = ArrayPool<T>.Shared.Rent(defaultCapacity);
            buffer = pooledArray;
        }
        size = 0;
    }
    
    /// <summary>
    /// Gets the number of items currently stored in the list.
    /// </summary>
    public int Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => size;
    }
    
    /// <summary>
    /// Gets a writable span over the items currently stored in the list.
    /// </summary>
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(buffer), size);
    }

    /// <summary>
    /// Gets writable memory over the items currently stored in the list.
    /// </summary>
    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => pooledArray is null ? Memory<T>.Empty : pooledArray.AsMemory(0, size);
    }
    
    /// <summary>
    /// Gets an array segment over the items currently stored in the list.
    /// </summary>
    public ArraySegment<T> ArraySegment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => pooledArray is null ? ArraySegment<T>.Empty : new(pooledArray, 0, size);
    }
    
    /// <summary>
    /// Returns an enumerable view of the items currently stored in the list.
    /// </summary>
    public IEnumerable<T> AsEnumerable()
    {
        return ArraySegment.AsEnumerable();
    }

    /// <summary>
    /// Adds an item to the end of the list, growing its storage when necessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var index = size;

        if((uint)index < (uint)buffer.Length)
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index) = item;
            size = index + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    /// <summary>
    /// Adds an item without checking that the list has spare capacity.
    /// </summary>
    /// <remarks>
    /// The caller must ensure capacity before calling this method, for example with <see cref="EnsureCapacity(int)"/>.
    /// Calling this method without spare capacity has undefined behavior and may corrupt memory.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddUnchecked(T item)
    {
        var index = size;
        Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index) = item;
        size = index + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        var index = size;
        Resize();
        Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index) = item;
        size = index + 1;
    }
   
    /// <summary>
    /// Removes the item at the specified index and preserves the order of the remaining items.
    /// </summary>
    public void RemoveAt(int i)
    {
        var index = size;
        if((uint)i >= (uint)index)
        {
            ThrowArgumentOutOfRangeException(index, i);
        }

        if(i < index - 1)
        {
            var s = buffer.Slice(i, index - i);
            s[1..].CopyTo(s);
        }

        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index - 1) = default!;
        }

        size = index - 1;
    }
    
    /// <summary>
    /// Removes the item at the specified index by moving the last item into its place.
    /// The order of remaining items is not preserved.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAtSwapBack(int i)
    {
        var index = size;
        if((uint)i >= (uint)index)
        {
            ThrowArgumentOutOfRangeException(index, i);
        }
        var lastIndex = index - 1;
        Unsafe.Add(ref MemoryMarshal.GetReference(buffer), i) = Unsafe.Add(ref MemoryMarshal.GetReference(buffer), lastIndex);

        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), lastIndex) = default!;
        }

        size = lastIndex;
    }

    /// <summary>
    /// Ensures that the list can hold the specified number of items without growing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if(capacity < 0)
        {
            ThrowArgumentOutOfRangeException(nameof(capacity));
        }

        if((uint)capacity > (uint)buffer.Length)
        {
            Resize(capacity);
        }
    }

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        var count = size;
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer[..count].Clear();
        }
        size = 0;
    }

    /// <summary>
    /// Adds all items from the specified span to the end of the list.
    /// </summary>
    public void AddRange(ReadOnlySpan<T> items)
    {
        var oldSize = size;
        if(items.Length > int.MaxValue - oldSize)
        {
            ThrowOutOfMemoryException();
        }

        var newSize = oldSize + items.Length;
        if((uint)newSize > (uint)buffer.Length)
        {
            AddRangeWithResize(items, oldSize, newSize);
        }
        else
        {
            items.CopyTo(buffer[oldSize..]);
            size = newSize;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddRangeWithResize(ReadOnlySpan<T> items, int oldSize, int newSize)
    {
        var newArray = ArrayPool<T>.Shared.Rent(newSize);
        buffer[..oldSize].CopyTo(newArray);
        items.CopyTo(newArray.AsSpan(oldSize));

        var oldArray = pooledArray;
        if(oldArray is not null)
        {
            ArrayPool<T>.Shared.Return(oldArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        pooledArray = newArray;
        buffer = newArray;
        size = newSize;
    }
    
    /// <summary>
    /// Finds the index of the specified item, or returns -1 when the item is not present.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        return Span.IndexOf(item);
    }
    
    /// <summary>
    /// Determines whether the list contains the specified item.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    /// <summary>
    /// Gets a reference to the item at the specified index.
    /// </summary>
    public ref T this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if((uint)i >= (uint)size)
            {
                ThrowIndexOutOfRangeException();
            }

            return ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), i);
        }
    }
    
    /// <summary>
    /// Returns the rented backing array to the shared array pool.
    /// </summary>
    public void Dispose()
    {
        var array = pooledArray;

        pooledArray = null;
        buffer = default;
        size = 0;

        if(array is null) return;

        ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    /// <summary>
    /// Returns an enumerator over the items currently stored in the list.
    /// </summary>
    public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize()
    {
        var oldLength = buffer.Length;
        var newSize = oldLength == 0 ? 16 : oldLength * 2;

        if(newSize < oldLength)
        {
            ThrowOutOfMemoryException();
        }

        Resize(newSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize(int newSize)
    {
        var newArray = ArrayPool<T>.Shared.Rent(newSize);
        buffer[..size].CopyTo(newArray);

        var oldArray = pooledArray;
        if(oldArray is not null)
        {
            ArrayPool<T>.Shared.Return(oldArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        pooledArray = newArray;
        buffer = newArray;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(int size, int i) =>
        throw new ArgumentOutOfRangeException(nameof(i), $"Index {i} is out of range. Valid range: 0-{size - 1}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfMemoryException() => throw new OutOfMemoryException();
}

/// <summary>
/// Provides factory methods for <see cref="TempList{T}"/>.
/// </summary>
public static class TempList
{
    /// <summary>
    /// Creates a list that contains the items from the specified span.
    /// </summary>
    public static TempList<T> Create<T>(ReadOnlySpan<T> items)
    {
        var list = new TempList<T>(items.Length);
        list.AddRange(items);
        return list;
    }
}
