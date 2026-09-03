# TempCollections benchmarks

`TempList<T>` と `List<T>` の、事前容量指定時・動的拡張時の追加、先頭削除、末尾交換削除を比較します。各ケースは要素数 16、256、1024 で実行され、メモリ割り当ても計測されます。

```powershell
dotnet run -c Release --project benchmarks/TempCollections.Benchmarks
```

特定のケースだけを実行するには、BenchmarkDotNet のフィルターを渡します。

```powershell
dotnet run -c Release --project benchmarks/TempCollections.Benchmarks -- --filter *Add*
```

結果は `BenchmarkDotNet.Artifacts` に出力されます（Git 管理対象外）。
