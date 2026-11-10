using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>mochizuki 0.0.8.3's optimized network: its int4 weights are made by the runtime at first use, so the
/// dlssnr-int4.bin an older build shipped comes out on the update.</summary>
public partial class MochizukiTests
{
    [Fact]
    public void AnUpdateTakesOutTheInt4WeightsAnOlderBuildShipped()
    {
        var game = Game("int4-weights");
        const string weightsPath = "dlssnr-amd/dlssnr-int4.bin";
        var (src, pins) = Staged("int4-weights-old", "0.4.0", [.. MochizukiPaths, weightsPath]);
        Assert.False(Work.Install(game, src, Preset.OptiScaler, pins, mochizuki: true).Failed);
        var weights = Path.Combine(game, "dlssnr-amd", "dlssnr-int4.bin");
        Assert.True(File.Exists(weights));

        var (newer, newerPins) = Staged("int4-weights-new", "0.4.1");
        Assert.DoesNotContain(weightsPath, newerPins.MochizukiFiles.Keys);
        var update = Work.Install(game, newer, Preset.OptiScaler, newerPins, mochizuki: true);
        Assert.False(update.Failed, update.ToLog("update"));
        Assert.False(File.Exists(weights), update.ToLog("update"));
        foreach (var destination in Destinations) Assert.True(File.Exists(Path.Combine(game, destination)), destination);
        var m = Manifest.Decode(File.ReadAllText(Path.Combine(game, Route.X64.ManifestFileName())));
        Assert.DoesNotContain(m.Entries, e => e.Name == weightsPath);
    }
}
