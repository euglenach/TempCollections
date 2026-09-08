# TempCollections benchmarks

`List<T>` と `TempCollections.TempList<T>`、および `HashSet<T>` と `TempCollections.TempHashSet<T>` を比較します。List は追加・削除、HashSet は事前容量指定時・動的拡張時の追加、構築後の検索・削除を計測します。各ケースは要素数 16、256、1024 で実行され、メモリ割り当ても計測されます。

```powershell
dotnet run -c Release --project benchmarks/TempCollections.Benchmarks
```

特定のケースだけを実行するには、BenchmarkDotNet のフィルターを渡します。

```powershell
dotnet run -c Release --project benchmarks/TempCollections.Benchmarks -- --filter *Add*
```

結果は `BenchmarkDotNet.Artifacts` に出力されます（Git 管理対象外）。
