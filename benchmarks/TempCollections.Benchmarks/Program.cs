using BenchmarkDotNet.Running;
using TempCollections.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(TempListBenchmarks).Assembly).Run(args);
