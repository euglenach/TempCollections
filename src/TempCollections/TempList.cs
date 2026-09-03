namespace TempCollections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides a stack-only, pooled, growable list for short-lived data.
/// </summary>
/// <remarks>
/// Dispose the list to return its rented array to the pool. This type is not thread-safe.
/// </remarks>
public ref struct TempList<T>
{
    private T[]? array;
    private int size;
    
    /// <summary>
    /// Initializes an empty list with the requested initial capacity.
    /// </summary>
    public TempList(int defaultCapacity)
    {
        array = defaultCapacity == 0 ? null : ArrayPool<T>.Shared.Rent(defaultCapacity);
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
        get => array is null ? Span<T>.Empty : array.AsSpan(0, size);
    }

    /// <summary>
    /// Gets writable memory over the items currently stored in the list.
    /// </summary>
    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => array is null ? Memory<T>.Empty : array.AsMemory(0, size);
    }
    
    /// <summary>
    /// Gets an array segment over the items currently stored in the list.
    /// </summary>
    public ArraySegment<T> ArraySegment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => array is null ? ArraySegment<T>.Empty : new(array, 0, size);
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
        // default
        if(array is null)
        {
            array = ArrayPool<T>.Shared.Rent(16);
            size = 0;
        }
        
        if(array.Length <= size)
        {
            Resize();
        }

        array[size++] = item;
    }
   
    /// <summary>
    /// Removes the item at the specified index and preserves the order of the remaining items.
    /// </summary>
    public void RemoveAt(int i)
    {
        if((uint)i >= (uint)size)
        {
            ThrowArgumentOutOfRangeException(size, i);
        }
        if(i < size)
        {
            Array.Copy(array!, i + 1, array!, i, size - i - 1);
        }
        size--;
        
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            array![size] = default!;
        }
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
        if(i < index - 1)
        {
            array![i] = array[index - 1];
        }
        size--;
    }
    
    /// <summary>
    /// Finds the index of the specified item, or returns -1 when the item is not present.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        return array is null ? -1 : Array.IndexOf(array, item, 0, size);
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
        get => ref Span[i];
    }
    
    /// <summary>
    /// Returns the rented backing array to the shared array pool.
    /// </summary>
    public void Dispose()
    {
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
        var newArray = ArrayPool<T>.Shared.Rent(size * 2);
        Array.Copy(array!, newArray, size);
        ArrayPool<T>.Shared.Return(array!, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        array = newArray;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(int size, int i) => 
        throw new ArgumentOutOfRangeException(nameof(i), $"Index {i} is out of range. Valid range: 0-{size - 1}");
}
