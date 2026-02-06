using BenchmarkDotNet.Running;
using ZMapper.Benchmarks;

// Use BenchmarkSwitcher to support CLI arguments like --filter, --job, etc.
// Examples:
//   dotnet run -c Release                              → runs all benchmarks
//   dotnet run -c Release -- --filter *Complex*        → runs only Complex benchmarks
//   dotnet run -c Release -- --filter *MapperBenchmark → runs only simple single benchmark
//   dotnet run -c Release -- --job short               → runs with fewer iterations (faster)
BenchmarkSwitcher
    .FromTypes([
        typeof(MapperBenchmark),           // Simple single object mapping
        typeof(BatchMapperBenchmark),      // Simple batch mapping (10/100/1000)
        typeof(ComplexMapperBenchmark),    // Complex single object (Order, Customer)
        typeof(ComplexBatchMapperBenchmark) // Complex batch mapping (10/100/1000)
    ])
    .Run(args);
