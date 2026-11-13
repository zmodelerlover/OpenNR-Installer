// lmxxf's own components: what leaving lmxxf out of a game takes away, and OptiScaler's package without it.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>lmxxf's runtime, kernels and shaders (LmxxfNrRuntime.dll, lmxxf-modules\, shaders\), taken out of
    /// OptiScaler's package so a game without lmxxf does not download them. It comes with <see cref="OptiScalerLeanComponent"/>,
    /// the package without them, which this app installs in place of <see cref="OptiScalerComponent"/>; the full package
    /// stays there for the installers already published, which never ask for a component they do not know.</summary>
    public const string LmxxfRuntimeComponent = "lmxxf-runtime";
    public const string OptiScalerLeanComponent = "optiscaler-lean";

    /// <summary>This manifest without lmxxf's weights and RX 9060 kernels, for a card lmxxf does not run on: about
    /// 590 MB nobody there can use. Neither is downloaded, pinned or installed then, and nor are person protection,
    /// which only lmxxf and mochizuki run, and WrongGPU and OpenRR, which run on RX 9000 only (OpenRR stays on an RX 7000,
    /// <paramref name="rx7000"/>, where it is experimental). NVIDIA's DLSS stays: danielblnc 0.8.0 reads it on every card.</summary>
    public PayloadManifest WithoutLmxxf(bool rx7000 = false) =>
        Without([.. LmxxfComponents, PersonModelComponent, WrongGpuComponent,
                 .. rx7000 ? Array.Empty<string>() : [OpenRrComponent, OpenRrDlssdComponent, OpenRrGfx11Component]]);

    /// <summary>lmxxf's weights, its Small model, its RX 9060 kernels and its runtime: what leaving lmxxf out takes away.</summary>
    public static readonly string[] LmxxfComponents =
        [LmxxfWeightsComponent, LmxxfSmallWeightsComponent, LmxxfGfx1200Component, LmxxfRuntimeComponent];

    /// <summary>OptiScaler's package without lmxxf in place of the full one, where both are there.</summary>
    private static Dictionary<string, PayloadComponent> Lean(Dictionary<string, PayloadComponent> components)
    {
        if (components.Remove(OptiScalerLeanComponent, out var lean)) components[OptiScalerComponent] = lean;
        return components;
    }
}
