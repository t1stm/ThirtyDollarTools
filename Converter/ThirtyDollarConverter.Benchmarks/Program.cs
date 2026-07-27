using BenchmarkDotNet.Running;
using ThirtyDollarConverter.Benchmarks;

// "probe" prints what the scenarios are made of, "verify" checks both incremental
// implementations against a full render, anything else runs the benchmarks.
switch (args.FirstOrDefault())
{
    case "probe":
        await Probe.Run();
        break;

    case "verify":
        await Verify.Run();
        break;

    default:
        BenchmarkSwitcher.FromAssembly(typeof(Workbench).Assembly).Run(args);
        break;
}
