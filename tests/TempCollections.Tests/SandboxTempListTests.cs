namespace TempCollections.Tests;

using System;
using System.Linq;
using SandboxTempList = TempCollections.Sandbox.TempList<int>;

public class SandboxTempListTests
{
    [Fact]
    public void DefaultInstance_ExposesAnEmptySpan()
    {
        SandboxTempList list = default;
        try
        {
            Assert.Empty(list.Span.ToArray());
            Assert.Equal(0, list.Size);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void MultipleLiveLists_DoNotShareABuffer()
    {
        var outer = new SandboxTempList(16);
        var inner = new SandboxTempList(16);
        try
        {
            outer.Add(10);
            inner.Add(20);
            outer.Add(30);

            Assert.Equal([10, 30], outer.Span.ToArray());
            Assert.Equal([20], inner.Span.ToArray());
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }
    }

    [Fact]
    public void Add_WithZeroCapacity_GrowsAndPreservesOrder()
    {
        var list = new SandboxTempList(0);
        try
        {
            for(var i = 0; i < 40; i++)
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
        var list = new SandboxTempList(0);
        try
        {
            list.EnsureCapacity(3);
            list.AddUnchecked(10);
            list.AddUnchecked(20);
            list.AddUnchecked(30);

            Assert.Equal(3, list.Size);
            Assert.Equal([10, 20, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void StackAllocatedInitialBuffer_SupportsSearchAndIndexing()
    {
        Span<int> initialBuffer = stackalloc int[4];
        var list = new SandboxTempList(initialBuffer);
        try
        {
            list.Add(10);
            list.Add(20);
            list.Add(30);

            ref var item = ref list[1];
            item = 25;

            Assert.Equal(1, list.IndexOf(25));
            Assert.True(list.Contains(30));
            Assert.Equal(-1, list.IndexOf(20));
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void AddRange_CanAppendItsOwnSpanWhileGrowing()
    {
        var list = new SandboxTempList(2);
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
    public void AddRange_CanAppendItsOwnSpanWithoutGrowing()
    {
        var list = new SandboxTempList(4);
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
    public void EnsureCapacityAndClear_WorkForAStackAllocatedBuffer()
    {
        Span<int> initialBuffer = stackalloc int[2];
        var list = new SandboxTempList(initialBuffer);
        try
        {
            list.EnsureCapacity(4);
            list.AddRange([10, 20, 30, 40]);
            list.Clear();

            Assert.Equal(0, list.Size);
            Assert.Empty(list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void RemoveMethods_KeepTheExpectedItems()
    {
        var list = new SandboxTempList(4);
        try
        {
            list.AddRange([10, 20, 30, 40]);
            list.RemoveAt(1);
            Assert.Equal([10, 30, 40], list.Span.ToArray());

            list.RemoveAtSwapBack(0);
            Assert.Equal([40, 30], list.Span.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void CollectionExpression_CreatesTheList()
    {
        TempCollections.Sandbox.TempList<int> list = [10, 20, 30];
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
    public void EnsureCapacity_WithNegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var list = new SandboxTempList(0);
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
    public void Indexer_UsesTheLogicalSizeForBoundsChecking()
    {
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var list = new SandboxTempList(1);
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
}
