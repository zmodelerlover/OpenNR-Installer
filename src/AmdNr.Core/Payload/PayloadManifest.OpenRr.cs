// OpenRR in the payload list (Work.OpenRr.cs): its runtime and kernels, and NVIDIA's nvngx_dlssd.dll 310.9.1.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>OpenRR, this project's Ray Reconstruction runtime for RX 9000: OpenNrRrRuntime.dll and its kernels, in
    /// opennr-rr.shaders/ in the payload and opennr-rr\shaders\ in the game. Inside an OptiScaler release only, and
    /// downloaded only when OpenRR is ticked.</summary>
    public const string OpenRrComponent = "openrr";

    /// <summary>NVIDIA's nvngx_dlssd.dll 310.9.1, the only one OpenRR builds its network from, for a game that has
    /// another or none (Work.OpenRrNeedsDlssd).</summary>
    public const string OpenRrDlssdComponent = "openrr-dlssd";

    private PayloadPins WithOpenRr(PayloadPins pins)
    {
        if (Components.TryGetValue(OpenRrComponent, out var rr))
        {
            pins.OpenRrFiles = rr.Installed.ToDictionary(f => f.RelativePath, f => Engine.Lower(f.Sha256), StringComparer.Ordinal);
            pins.OpenRrVersion = rr.Version;
        }
        if (Components.TryGetValue(OpenRrDlssdComponent, out var dlssd) && dlssd.Installed.FirstOrDefault() is { } file)
            (pins.OpenRrDlssd, pins.OpenRrDlssdSha) = (file.RelativePath, Engine.Lower(file.Sha256));
        return pins;
    }
}
