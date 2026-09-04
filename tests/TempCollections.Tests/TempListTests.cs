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
    public void AddUnchecked_WithReservedCapacity_AppendsItems()
    {
        var list = new TempList<int>(0);
        try
        {
            list.EnsureCapacity(3);
            list.AddUnchecked(10);
            list.AddUnchecked(20);
            list.AddUnchecked(30);

            Assert.Equal([10, 20, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void AddRange_CanAppendItsOwnSpanWhileGrowing()
    {
        var list = new TempList<int>(0);
        try
        {
            list.AddRange(Enumerable.Range(0, 40).ToArray());
            list.AddRange(list.Span);

            Assert.Equal(Enumerable.Range(0, 40).Concat(Enumerable.Range(0, 40)), list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void AddRange_CanAppendItsOwnSpanWithoutGrowing()
    {
        var list = new TempList<int>(4);
        try
        {
            list.Add(10);
            list.Add(20);
            list.AddRange(list.Span);

            Assert.Equal([10, 20, 10, 20], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacityAndClear_ResetTheList()
    {
        var list = new TempList<int>(0);
        try
        {
            list.EnsureCapacity(4);
            list.AddRange([10, 20, 30, 40]);
            list.Clear();

            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
            Assert.Empty(list.Memory.Span.ToArray());
            Assert.Equal(0, list.ArraySegment.Count);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacity_WithNegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var list = new TempList<int>(0);
            try
            {
                list.EnsureCapacity(-1);
            }
            finally
            {
                list.Dispose();
            }
        });
    }

    [Fact]
    public void CollectionExpression_CreatesTheList()
    {
        TempList<int> list = [10, 20, 30];
        try
        {
            Assert.Equal([10, 20, 30], list.Span.ToArray());
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
    public void RemoveRange_ShiftsRemainingItemsAndPreservesTheirOrder()
    {
        var list = CreateList(10, 20, 30, 40, 50, 60);
        try
        {
            list.RemoveRange(2, 3);

            Assert.Equal(3, list.Size);
            Assert.Equal([10, 20, 60], list.Span.ToArray());

            list.RemoveRange(list.Size, 0);
            Assert.Equal([10, 20, 60], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveRange_ThrowsForAnInvalidRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRange(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRange(4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRange(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRange(2, 2));

        static void RemoveRange(int index, int count)
        {
            var list = new TempList<int>(3);
            try
            {
                list.AddRange([10, 20, 30]);
                list.RemoveRange(index, count);
            }
            finally
            {
                list.Dispose();
            }
        }
    }

    [Fact]
    public void RemoveAll_RemovesMatchingItemsPreservesOrderAndReturnsTheirCount()
    {
        var list = CreateList(10, 21, 30, 41, 50, 61);
        try
        {
            var removedCount = list.RemoveAll(static value => value % 10 == 0);

            Assert.Equal(3, removedCount);
            Assert.Equal([21, 41, 61], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAll_WithNoMatches_DoesNotModifyTheList()
    {
        var list = CreateList(10, 20, 30);
        try
        {
            var removedCount = list.RemoveAll(static value => value < 0);

            Assert.Equal(0, removedCount);
            Assert.Equal([10, 20, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAll_WithAllMatches_ClearsTheList()
    {
        var list = CreateList(10, 20, 30);
        try
        {
            var removedCount = list.RemoveAll(static _ => true);

            Assert.Equal(3, removedCount);
            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAll_ClearsVacatedReferenceSlots()
    {
        var list = new TempList<string>(4);
        try
        {
            list.AddRange(new[] { "a", "b", "c", "d" });

            Assert.Equal(2, list.RemoveAll(static value => value is "b" or "d"));
            Assert.Equal(["a", "c"], list.Span.ToArray());
            Assert.Null(list.ArraySegment.Array![2]);
            Assert.Null(list.ArraySegment.Array![3]);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAll_WithANullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(RemoveAllWithNullPredicate);

        static void RemoveAllWithNullPredicate()
        {
            var list = new TempList<int>(1);
            try
            {
                list.RemoveAll(null!);
            }
            finally
            {
                list.Dispose();
            }
        }
    }

    [Fact]
    public void RemoveAllSwapBack_RemovesMatchingItemsAndReturnsTheirCount()
    {
        var list = CreateList(10, 21, 30, 41, 50, 61);
        var predicateCalls = 0;
        try
        {
            var removedCount = list.RemoveAllSwapBack(value =>
            {
                predicateCalls++;
                return value % 10 == 0;
            });

            Assert.Equal(3, removedCount);
            Assert.Equal(6, predicateCalls);
            Assert.Equal([61, 21, 41], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAllSwapBack_WithNoMatches_DoesNotModifyTheList()
    {
        var list = CreateList(10, 20, 30);
        try
        {
            var removedCount = list.RemoveAllSwapBack(static value => value < 0);

            Assert.Equal(0, removedCount);
            Assert.Equal([10, 20, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAllSwapBack_WithAllMatches_ClearsTheList()
    {
        var list = CreateList(10, 20, 30);
        try
        {
            var removedCount = list.RemoveAllSwapBack(static _ => true);

            Assert.Equal(3, removedCount);
            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAllSwapBack_ClearsVacatedReferenceSlots()
    {
        var list = new TempList<string>(4);
        try
        {
            list.AddRange(new[] { "a", "b", "c", "d" });

            Assert.Equal(2, list.RemoveAllSwapBack(static value => value is "b" or "d"));
            Assert.Equal(["a", "c"], list.Span.ToArray());
            Assert.Null(list.ArraySegment.Array![2]);
            Assert.Null(list.ArraySegment.Array![3]);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveAllSwapBack_WithANullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(RemoveAllSwapBackWithNullPredicate);

        static void RemoveAllSwapBackWithNullPredicate()
        {
            var list = new TempList<int>(1);
            try
            {
                list.RemoveAllSwapBack(null!);
            }
            finally
            {
                list.Dispose();
            }
        }
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
    public void RemoveRangeSwapBack_FillsTheGapWithTailItems()
    {
        var list = CreateList(10, 20, 30, 40, 50, 60);
        try
        {
            list.RemoveRangeSwapBack(1, 2);

            Assert.Equal(4, list.Size);
            Assert.Equal([10, 50, 60, 40], list.Span.ToArray());

            list.RemoveRangeSwapBack(3, 1);
            Assert.Equal([10, 50, 60], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveRangeSwapBack_HandlesRangesThatOverlapTheTail()
    {
        var list = CreateList(10, 20, 30, 40, 50, 60);
        try
        {
            list.RemoveRangeSwapBack(3, 2);

            Assert.Equal([10, 20, 30, 60], list.Span.ToArray());

            list.RemoveRangeSwapBack(3, 1);
            Assert.Equal([10, 20, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveRangeSwapBack_ThrowsForAnInvalidRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRangeSwapBack(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRangeSwapBack(4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRangeSwapBack(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoveRangeSwapBack(2, 2));

        static void RemoveRangeSwapBack(int index, int count)
        {
            var list = new TempList<int>(3);
            try
            {
                list.AddRange([10, 20, 30]);
                list.RemoveRangeSwapBack(index, count);
            }
            finally
            {
                list.Dispose();
            }
        }
    }

    [Fact]
    public void RangeRemovals_ClearVacatedReferenceSlots()
    {
        var ordered = new TempList<string>(4);
        var unordered = new TempList<string>(4);
        try
        {
            ordered.AddRange(new[] { "a", "b", "c", "d" });
            ordered.RemoveRange(1, 2);
            Assert.Equal(["a", "d"], ordered.Span.ToArray());
            Assert.Null(ordered.ArraySegment.Array![2]);
            Assert.Null(ordered.ArraySegment.Array![3]);

            unordered.AddRange(new[] { "a", "b", "c", "d" });
            unordered.RemoveRangeSwapBack(1, 2);
            Assert.Equal(["a", "d"], unordered.Span.ToArray());
            Assert.Null(unordered.ArraySegment.Array![2]);
            Assert.Null(unordered.ArraySegment.Array![3]);
        }
        finally
        {
            unordered.Dispose();
            ordered.Dispose();
        }
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
