// HexNeural in the payload list (Work.HexNeural.cs): Emion's RX 6000 NR runtime, its HIP network and its kernels.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>HexNeural, Emion's NR runtime for RX 6000 cards (RDNA2, HIP 6.4, GPL): Rdna2NrRuntime.dll on the lmxxf
    /// interface, nr_runtime.dll, Rdna2NrRuntime.ini and the kernels and plans in nr_data_h15\ (no weights: the
    /// runtime reads them from the game's own nvngx_dlssnr.dll). Inside an OptiScaler release only, from the one whose
    /// OptiScaler knows NrBackend=rdna2, and downloaded only when ticked on an RX 6000 card.</summary>
    public const string HexNeuralComponent = "hexneural";

    private PayloadPins WithHexNeural(PayloadPins pins)
    {
        if (!Components.TryGetValue(HexNeuralComponent, out var hex)) return pins;
        pins.HexNeuralFiles = hex.Installed.ToDictionary(f => f.RelativePath, f => Engine.Lower(f.Sha256), StringComparer.Ordinal);
        pins.HexNeuralVersion = hex.Version;
        return pins;
    }
}
