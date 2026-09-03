namespace TempCollections.Sandbox;

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[CollectionBuilder(typeof(TempList), nameof(TempList.Create))]
public ref struct TempList<T>
{
    private Span<T> buffer;
    private T[]? pooledArray;
    private int size;

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

    public TempList(Span<T> initialBuffer)
    {
        pooledArray = null;
        buffer = initialBuffer;
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
        get => buffer[..size];
    }
    
    public ref T this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span[i];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var index = size;
        var b = buffer;
        
        if((uint)index < (uint)b.Length)
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(b), index) = item;
            size = index + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Resize();
        Unsafe.Add(ref MemoryMarshal.GetReference(buffer), size++) = item;
    }

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
            var b = buffer;
            Unsafe.Add(ref MemoryMarshal.GetReference(b), i) = Unsafe.Add(ref MemoryMarshal.GetReference(b), index - 1);
        }

        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index - 1) = default!;
        }

        size--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        if(newSize < size) newSize = size;
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer[..size].Clear();
        }
        size = 0;
    }
    
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
        }
        else
        {
            items.CopyTo(buffer[oldSize..]);
        }

        size = newSize;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        return buffer[..size].IndexOf(item);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }
    
    public void Dispose()
    {
        var array = pooledArray;

        pooledArray = null;
        buffer = default;
        size = 0;

        if(array is null) return;

        ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(int size, int i) =>
        throw new ArgumentOutOfRangeException(nameof(i), $"Index {i} is out of range. Valid range: 0-{size - 1}");
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfMemoryException() => throw new OutOfMemoryException();
}

public static class TempList
{
    public static TempList<T> Create<T>(ReadOnlySpan<T> items)
    {
        var list = new TempList<T>(items.Length);
        list.AddRange(items);
        return list;
    }
}
