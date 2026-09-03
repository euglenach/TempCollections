using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using TempCollections.Benchmarks;

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.Default.WithMsBuildArguments("-m:1"));

BenchmarkSwitcher.FromAssembly(typeof(TempListBenchmarks).Assembly).Run(args, config);
