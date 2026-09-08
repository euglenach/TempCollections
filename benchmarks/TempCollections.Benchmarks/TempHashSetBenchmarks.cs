namespace TempCollections.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Default)]
[RankColumn]
[WarmupCount(3)]
[IterationCount(10)]
public class TempHashSetBenchmarks
{
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
    public int HashSet_AddWithCapacity()
    {
        var set = new HashSet<int>(Count);
        foreach (var value in values)
        {
            set.Add(value);
        }

        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add: preallocated")]
    public int TempHashSet_AddWithCapacity()
    {
        var set = new TempHashSet<int>(Count);
        try
        {
            foreach (var value in values)
            {
                set.Add(value);
            }

            return set.Count;
        }
        finally
        {
            set.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add: growing")]
    public int HashSet_AddWithGrowth()
    {
        var set = new HashSet<int>();
        foreach (var value in values)
        {
            set.Add(value);
        }

        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add: growing")]
    public int TempHashSet_AddWithGrowth()
    {
        var set = new TempHashSet<int>(0);
        try
        {
            foreach (var value in values)
            {
                set.Add(value);
            }

            return set.Count;
        }
        finally
        {
            set.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Build and Contains")]
    public int HashSet_BuildAndContains()
    {
        var set = new HashSet<int>(values);
        var foundCount = 0;
        foreach (var value in values)
        {
            if(set.Contains(value))
            {
                foundCount++;
            }
        }

        return foundCount;
    }

    [Benchmark]
    [BenchmarkCategory("Build and Contains")]
    public int TempHashSet_BuildAndContains()
    {
        var set = new TempHashSet<int>(Count);
        try
        {
            foreach (var value in values)
            {
                set.Add(value);
            }

            var foundCount = 0;
            foreach (var value in values)
            {
                if(set.Contains(value))
                {
                    foundCount++;
                }
            }

            return foundCount;
        }
        finally
        {
            set.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Build and Remove")]
    public int HashSet_BuildAndRemove()
    {
        var set = new HashSet<int>(values);
        foreach (var value in values)
        {
            set.Remove(value);
        }

        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Build and Remove")]
    public int TempHashSet_BuildAndRemove()
    {
        var set = new TempHashSet<int>(Count);
        try
        {
            foreach (var value in values)
            {
                set.Add(value);
            }

            foreach (var value in values)
            {
                set.Remove(value);
            }

            return set.Count;
        }
        finally
        {
            set.Dispose();
        }
    }
}
