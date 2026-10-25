using BenchmarkDotNet.Running;
using ThirtyDollarConverter.Benchmarks;

// "probe" prints what the scenarios are made of, "verify" checks the incremental renderer
// against a full render and the voice mixer against the legacy one, anything else runs the
// benchmarks.
switch (args.FirstOrDefault())
{
    case "probe":
        await Probe.Run();
        break;

    case "tau":
        await TauProbe.Run(args[1]);
        break;

    case "tau-feeder":
        await TauProbe.Feeder(args[1]);
        break;

    case "verify":
        await Verify.Run();
        break;

    default:
        BenchmarkSwitcher.FromAssembly(typeof(Workbench).Assembly).Run(args);
        break;
}