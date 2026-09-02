namespace TempCollections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public ref struct TempList<T>
{
    private T[] array;
    private int size;
    
    public TempList(int defaultCapacity)
    {
        array = ArrayPool<T>.Shared.Rent(defaultCapacity);
        size = 0;
    }
    
    public int Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => size;
    }
    
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => array.AsSpan(0, size);
    }

    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => array.AsMemory(0, size);
    }
    
    public ArraySegment<T> ArraySegment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(array, 0, size);
    }
    
    public IEnumerable<T> AsEnumerable()
    {
        return ArraySegment.AsEnumerable();
    }

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
   
    public void RemoveAt(int i)
    {
        if((uint)i >= (uint)size)
        {
            ThrowArgumentOutOfRangeException(size, i);
        }
        if(i < size)
        {
            Array.Copy(array, i + 1, array, i, size - i - 1);
        }
        size--;
        
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            array[size] = default!;
        }
    }
    
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
            array[i] = array[index - 1];
        }
        size--;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        return Array.IndexOf(array, item, 0, size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    public ref T this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span[i];
    }
    
    public void Dispose()
    {
        if(array is null) return;
        ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize()
    {
        var newArray = ArrayPool<T>.Shared.Rent(size * 2);
        Array.Copy(array, newArray, size);
        ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        array = newArray;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(int size, int i) => 
        throw new ArgumentOutOfRangeException(nameof(i), $"Index {i} is out of range. Valid range: 0-{size - 1}");
}