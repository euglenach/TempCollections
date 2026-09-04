namespace TempCollections.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Default)]
[RankColumn]
[WarmupCount(3)]
[IterationCount(10)]
public class TempListBenchmarks
{
    private static readonly Predicate<int> IsEven = static value => (value & 1) == 0;

    private int[] values = [];

    [Params(16, 256, 1024)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        values = Enumerable.Range(0, Count).ToArray();
    }

    [Benchmark]
    [BenchmarkCategory("Add: preallocated")]
    public int List_AddWithCapacity()
    {
        var list = new List<int>(Count);
        foreach (var value in values)
        {
            list.Add(value);
        }

        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add: preallocated")]
    public int TempList_AddWithCapacity()
    {
        var list = new TempList<int>(Count);
        try
        {
            foreach (var value in values)
            {
                list.Add(value);
            }

            return list.Size;
        }
        finally
        {
            list.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("AddRange: preallocated")]
    public int List_AddRangeWithCapacity()
    {
        var list = new List<int>(Count);
        list.AddRange(values);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add: growing")]
    public int List_AddWithGrowth()
    {
        var list = new List<int>();
        foreach (var value in values)
        {
            list.Add(value);
        }

        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add: growing")]
    public int TempList_AddWithGrowth()
    {
        var list = new TempList<int>(0);
        try
        {
            foreach (var value in values)
            {
                list.Add(value);
            }

            return list.Size;
        }
        finally
        {
            list.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAt: front")]
    public int List_RemoveAtFront()
    {
        var list = new List<int>(values);
        var sum = 0;
        while (list.Count > 0)
        {
            sum += list[0];
            list.RemoveAt(0);
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAt: front")]
    public int TempList_RemoveAtFront()
    {
        var list = new TempList<int>(Count);
        try
        {
            list.AddRange(values);

            var sum = 0;
            while (list.Size > 0)
            {
                sum += list[0];
                list.RemoveAt(0);
            }

            return sum;
        }
        finally
        {
            list.Dispose();
        }
    }
    
    [Benchmark]
    [BenchmarkCategory("RemoveAt: swap back")]
    public int List_RemoveAtSwapBack()
    {
        var list = new List<int>(values);
        var sum = 0;
        while (list.Count > 0)
        {
            sum += list[0];
            var lastIndex = list.Count - 1;
            list[0] = list[lastIndex];
            list.RemoveAt(lastIndex);
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAt: swap back")]
    public int TempList_RemoveAtSwapBack()
    {
        var list = new TempList<int>(Count);
        try
        {
            list.AddRange(values);

            var sum = 0;
            while (list.Size > 0)
            {
                sum += list[0];
                list.RemoveAtSwapBack(0);
            }

            return sum;
        }
        finally
        {
            list.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAll: half")]
    public int List_RemoveAllEven()
    {
        var list = new List<int>(values);
        return list.RemoveAll(IsEven);
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAll: half")]
    public int TempList_RemoveAllEven()
    {
        var list = new TempList<int>(Count);
        try
        {
            list.AddRange(values);
            return list.RemoveAll(IsEven);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAll: half, swap back")]
    public int TempList_RemoveAllSwapBackEven()
    {
        var list = new TempList<int>(Count);
        try
        {
            list.AddRange(values);
            return list.RemoveAllSwapBack(IsEven);
        }
        finally
        {
            list.Dispose();
        }
    }
}
