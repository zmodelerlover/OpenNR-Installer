// A 32-bit game's OptiScaler (0.5.2 and later): its 32-bit proxy beside the game hands every frame to a
// 64-bit host in OptiScaler64\, which runs OptiScaler with everything a 64-bit game gets beside it.

namespace AmdNr.Core;

public static partial class Work
{
    /// <summary>The first OptiScaler release that runs 64-bit D3D10 games and, through its 32-bit proxies (x86/ in
    /// its package) and their 64-bit host, 32-bit D3D9 and D3D10 games (<see cref="GraphicsDetection.OptiX86Apis"/>).</summary>
    public static readonly Version OptiBridges = new(0, 5, 2);

    /// <summary>Whether this OptiScaler version runs D3D10 and the 32-bit games.</summary>
    public static bool OptiRunsBridged(string? version) =>
        version is not null && AddonReleases.Version(version) is { } v && v >= OptiBridges;

    /// <summary>The first OptiScaler release whose 32-bit proxies also take 32-bit D3D11 (x86\dxgi.dll) and OpenGL
    /// (x86\opengl32.dll) games, on the same host (<see cref="GraphicsDetection.OptiX86WideApis"/>).</summary>
    public static readonly Version OptiX86Wide = new(0, 5, 4);

    /// <summary>Whether this OptiScaler version runs 32-bit D3D11 and OpenGL games.</summary>
    public static bool OptiRunsX86Wide(string? version) =>
        version is not null && AddonReleases.Version(version) is { } v && v >= OptiX86Wide;

    /// <summary>A 32-bit game's OptiScaler: everything a 64-bit game gets beside it goes in this folder, where the
    /// 64-bit host loads it; the 32-bit proxy goes beside the game.</summary>
    internal const string OptiHostFolder = "OptiScaler64";

    /// <summary>The 64-bit host the 32-bit proxies start, and the folder of the package that holds it and them.</summary>
    internal const string OptiHostName = "OptiScalerDx9Host.exe";
    private const string OptiX86Payload = "x86/";

    /// <summary>The 32-bit proxy for a 32-bit game's API: d3d9.dll for D3D9, opengl32.dll for OpenGL, dxgi.dll (which
    /// D3D10 and D3D11 make their swap chains through) for the others.</summary>
    internal static string OptiX86Proxy(GraphicsApi? api) => api switch
    {
        GraphicsApi.D3D9 => "d3d9.dll",
        GraphicsApi.OpenGL => "opengl32.dll",
        _ => "dxgi.dll",
    };

    /// <summary>Why this OptiScaler version cannot take this 32-bit game, or null when it can.</summary>
    private static string? X86Refused(GraphicsDetection detected, GraphicsApi? api, string version)
    {
        if (!OptiRunsBridged(version))
            return $"This is a 32-bit game. OptiScaler runs 32-bit games from {OptiBridges} on, through its 32-bit "
                   + "proxy: pick that version or a later one, or the ReShade route.";
        if (OptiRunsX86Wide(version)) return null;
        var on = api ?? detected.OptiApi(true, true) ?? detected.All.FirstOrDefault(a => a is GraphicsApi.D3D11 or GraphicsApi.OpenGL);
        return on is GraphicsApi.D3D11 or GraphicsApi.OpenGL
            ? $"This is a 32-bit {(on == GraphicsApi.OpenGL ? "OpenGL" : "D3D11")} game. OptiScaler runs those from {OptiX86Wide} "
              + "on: pick that version or a later one, or the ReShade route."
            : null;
    }

    /// <summary>The API a 32-bit game's OptiScaler runs on with this version.</summary>
    private static GraphicsApi? X86Api(GraphicsDetection detected, GraphicsApi? api, string version) =>
        api ?? detected.OptiApi(true, true, OptiRunsX86Wide(version));

    /// <summary>A 32-bit game has no upscaler for OptiScaler to take over: a fresh OptiScaler.ini there runs NR on
    /// the finished frame from the start, NR switched on.</summary>
    private static byte[] NrOn(byte[] ini) =>
        System.Text.Encoding.UTF8.GetBytes(Engine.SetIni(System.Text.Encoding.UTF8.GetString(ini), "DlssNr", "Enabled", "true"));

    /// <summary>What a 32-bit game's host never loads: lmxxf, which does not run without an upscaler, and its data.</summary>
    private static bool LmxxfOnly(string path) =>
        path == LmxxfRuntimeName || path.StartsWith("lmxxf-modules", StringComparison.Ordinal)
        || path.StartsWith(LmxxfWeightsFolder, StringComparison.Ordinal) || path.StartsWith("shaders/", StringComparison.Ordinal);
}
