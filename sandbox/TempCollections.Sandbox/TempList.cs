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
        get => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(buffer), size);
    }
    
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

    /// <summary>空き容量があることを前提に、容量確認をせず要素を追加します。</summary>
    /// <remarks>
    /// 呼び出し側で <see cref="EnsureCapacity(int)"/> などにより容量を確保してから使用してください。
    /// 容量不足で呼び出した場合の動作は未定義で、メモリ破壊を引き起こす可能性があります。
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
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), i) = Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index - 1);
        }

        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index - 1) = default!;
        }

        size = index - 1;
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        return Span.IndexOf(item);
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
    private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

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
