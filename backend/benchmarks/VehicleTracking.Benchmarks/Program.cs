using BenchmarkDotNet.Running;

namespace VehicleTracking.Benchmarks;

// API projesindeki Program tipiyle çakışmaması için ayrı ad alanında tutulur.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (!File.Exists(BenchmarkData.DataPath))
        {
            Console.Error.WriteLine(BenchmarkData.MissingDataMessage);
            return 1;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
