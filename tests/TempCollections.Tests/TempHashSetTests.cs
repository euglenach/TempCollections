namespace TempCollections.Tests;

using System;
using System.Collections.Generic;

public class TempHashSetTests
{
    [Fact]
    public void Constructor_StartsEmpty()
    {
        var set = new TempHashSet<int>(4);
        try
        {
            Assert.Equal(0, set.Count);
            Assert.False(set.Contains(10));
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TempHashSet<int>(-1));
    }

    [Fact]
    public void Add_StoresOnlyUniqueItemsAndGrows()
    {
        var set = new TempHashSet<int>(1);
        try
        {
            for(var value = 0; value < 40; value++)
            {
                Assert.True(set.Add(value));
            }

            Assert.False(set.Add(10));
            Assert.Equal(40, set.Count);
            for(var value = 0; value < 40; value++)
            {
                Assert.True(set.Contains(value));
            }
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void DefaultInstance_AddInitializesTheSet()
    {
        TempHashSet<int> set = default;
        try
        {
            Assert.True(set.Add(42));
            Assert.Equal(1, set.Count);
            Assert.True(set.Contains(42));
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void CustomComparer_DeterminesItemIdentity()
    {
        var set = new TempHashSet<string>(0, StringComparer.OrdinalIgnoreCase);
        try
        {
            Assert.True(set.Add("temp"));
            Assert.False(set.Add("TEMP"));
            Assert.True(set.Contains("TeMp"));
            Assert.Same(StringComparer.OrdinalIgnoreCase, set.Comparer);
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void Remove_UnlinksCollisionsAndReusesFreedSlots()
    {
        var set = new TempHashSet<int>(0, new CollidingIntComparer());
        try
        {
            Assert.True(set.Add(10));
            Assert.True(set.Add(20));
            Assert.True(set.Add(30));

            Assert.True(set.Remove(20));
            Assert.False(set.Contains(20));
            Assert.True(set.Contains(10));
            Assert.True(set.Contains(30));
            Assert.Equal(2, set.Count);

            Assert.True(set.Add(40));
            Assert.True(set.Contains(40));
            Assert.Equal(3, set.Count);
            Assert.False(set.Remove(99));
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void Clear_RemovesAllItemsAndTheSetRemainsUsable()
    {
        var set = new TempHashSet<string>(4);
        try
        {
            set.Add("a");
            set.Add("b");
            set.Clear();

            Assert.Equal(0, set.Count);
            Assert.False(set.Contains("a"));
            Assert.True(set.Add("c"));
            Assert.True(set.Contains("c"));
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void CollectionExpression_CreatesASetWithUniqueItems()
    {
        TempHashSet<int> set = [10, 20, 10, 30];
        try
        {
            Assert.Equal(3, set.Count);
            Assert.True(set.Contains(10));
            Assert.True(set.Contains(20));
            Assert.True(set.Contains(30));
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void GetEnumerator_VisitsEachLiveItemOnce()
    {
        var set = new TempHashSet<int>(4);
        try
        {
            set.Add(10);
            set.Add(20);
            set.Add(30);
            set.Remove(20);

            var items = new List<int>();
            foreach(var item in set)
            {
                items.Add(item);
            }

            Assert.Equal(2, items.Count);
            Assert.Contains(10, items);
            Assert.Contains(30, items);
        }
        finally
        {
            set.Dispose();
        }
    }

    [Fact]
    public void Dispose_OnDefaultInstance_IsANoOp()
    {
        TempHashSet<int> set = default;

        set.Dispose();

        Assert.Equal(0, set.Count);
    }

    private sealed class CollidingIntComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;

        public int GetHashCode(int obj) => 0;
    }
}
