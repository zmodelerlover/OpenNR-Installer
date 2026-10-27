// The two NVIDIA downloads behind danielblnc's DLSS frame generation in a game without its own (Work.HostFg.cs).

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>NVIDIA's Streamline SDK release from NVIDIA-RTX/Streamline: the archive, with the production
    /// sl.interposer, sl.common, sl.dlss_g, sl.reflex and sl.pcl of its bin\x64 taken out into streamline\. Inside a
    /// release only, and downloaded only when danielblnc's DLSS-FG is ticked.</summary>
    public const string StreamlineComponent = "streamline";

    /// <summary>NVIDIA's nvngx_dlssg.dll 310.9.1 from NVIDIA's own GitHub repository, for OptiScaler\.</summary>
    public const string DlssgComponent = "dlssg";

    private PayloadPins WithHostFg(PayloadPins pins)
    {
        if (Components.TryGetValue(StreamlineComponent, out var streamline))
            pins.StreamlineFiles = streamline.Installed.ToDictionary(f => f.RelativePath, f => Engine.Lower(f.Sha256), StringComparer.Ordinal);
        if (Components.TryGetValue(DlssgComponent, out var dlssg) && dlssg.Installed.FirstOrDefault() is { } file)
            (pins.Dlssg, pins.DlssgSha) = (file.RelativePath, Engine.Lower(file.Sha256));
        return pins;
    }
}
