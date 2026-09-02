namespace TempCollections.Tests;

using System;
using System.Linq;

public class TempListTests
{
    [Fact]
    public void Constructor_StartsEmpty()
    {
        var list = new TempList<int>(4);
        try
        {
            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
            Assert.Empty(list.Memory.Span.ToArray());
            Assert.Equal(0, list.ArraySegment.Count);
            Assert.Empty(list.AsEnumerable());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TempList<int>(-1));
    }

    [Fact]
    public void Add_WithZeroCapacity_GrowsAndStoresItem()
    {
        var list = new TempList<int>(0);
        try
        {
            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
            Assert.Equal(0, list.ArraySegment.Count);

            list.Add(42);

            Assert.Equal(1, list.Size);
            Assert.Equal(42, list[0]);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Add_OnDefaultInstance_InitializesTheList()
    {
        TempList<int> list = default;
        try
        {
            list.Add(42);

            Assert.Equal(1, list.Size);
            Assert.Equal([42], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void DefaultInstance_ActsAsAnEmptyList()
    {
        TempList<string> list = default;
        try
        {
            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
            Assert.Empty(list.Memory.Span.ToArray());
            Assert.Equal(0, list.ArraySegment.Count);
            Assert.Empty(list.AsEnumerable());
            Assert.Equal(-1, list.IndexOf("missing"));
            Assert.False(list.Contains("missing"));
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Add_PreservesOrderAndGrowsBeyondInitialCapacity()
    {
        var list = new TempList<int>(1);
        try
        {
            for (var i = 0; i < 40; i++)
            {
                list.Add(i);
            }

            Assert.Equal(40, list.Size);
            Assert.Equal(Enumerable.Range(0, 40), list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Views_ExposeExactlyTheAddedItems()
    {
        var list = new TempList<int>(3);
        try
        {
            list.Add(10);
            list.Add(20);
            list.Add(30);

            Assert.Equal([10, 20, 30], list.Span.ToArray());
            Assert.Equal([10, 20, 30], list.Memory.Span.ToArray());
            Assert.Equal([10, 20, 30], list.ArraySegment);
            Assert.Equal([10, 20, 30], list.AsEnumerable());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Indexer_ReturnsAWritableReference()
    {
        var list = new TempList<int>(1);
        try
        {
            list.Add(10);
            ref var item = ref list[0];
            item = 99;

            Assert.Equal(99, list[0]);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Indexer_ThrowsForAnInvalidIndex()
    {
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var list = new TempList<int>(1);
            try
            {
                _ = list[0];
            }
            finally
            {
                list.Dispose();
            }
        });
    }

    [Fact]
    public void IndexOfAndContains_UseTheLogicalItemsOnly()
    {
        var list = new TempList<string>(1);
        try
        {
            list.Add("first");
            list.Add("second");
            list.Add("first");

            Assert.Equal(0, list.IndexOf("first"));
            Assert.Equal(1, list.IndexOf("second"));
            Assert.Equal(-1, list.IndexOf("missing"));
            Assert.True(list.Contains("second"));
            Assert.False(list.Contains("missing"));
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAt_ShiftsFollowingItemsAndDecreasesSize()
    {
        var list = CreateList(10, 20, 30, 40);
        try
        {
            list.RemoveAt(1);

            Assert.Equal(3, list.Size);
            Assert.Equal([10, 30, 40], list.Span.ToArray());

            list.RemoveAt(2);
            Assert.Equal([10, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void RemoveAt_ThrowsForAnInvalidIndex(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var list = new TempList<int>(1);
            try
            {
                list.RemoveAt(index);
            }
            finally
            {
                list.Dispose();
            }
        });
    }

    [Fact]
    public void RemoveAtSwapBack_ReplacesRemovedItemWithLastItem()
    {
        var list = CreateList(10, 20, 30, 40);
        try
        {
            list.RemoveAtSwapBack(1);

            Assert.Equal(3, list.Size);
            Assert.Equal([10, 40, 30], list.Span.ToArray());

            list.RemoveAtSwapBack(2);
            Assert.Equal([10, 40], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void RemoveAtSwapBack_ThrowsForAnInvalidIndex(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var list = new TempList<int>(1);
            try
            {
                list.RemoveAtSwapBack(index);
            }
            finally
            {
                list.Dispose();
            }
        });
    }

    [Fact]
    public void GetEnumerator_SupportsForeach()
    {
        var list = CreateList(10, 20, 30);
        try
        {
            var sum = 0;
            foreach (var item in list)
            {
                sum += item;
            }

            Assert.Equal(60, sum);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void Dispose_OnDefaultInstance_IsANoOp()
    {
        TempList<int> list = default;

        list.Dispose();

        Assert.Equal(0, list.Size);
    }

    private static TempList<int> CreateList(params int[] items)
    {
        var list = new TempList<int>(items.Length);
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }
}
