// What an OptiScaler install would meet in this folder, said before anything is written (Work.OptiScaler.cs).

namespace AmdNr.Core;

public static partial class Work
{
    private static Report PreflightOptiScaler(string gameDir, string payloadDir, PayloadPins pins, string? proxy,
        bool mochizuki, string? ownRuntime, UserRuntime? wantedRuntime, string? suggestedProxy, GraphicsApi? api, bool wrongGpu,
        bool daniel = true)
    {
        var report = new Report();
        var dir = ResolveSource(gameDir);
        var src = ResolveSource(payloadDir);

        if (pins.OptiFiles.Count == 0 || pins.OptiRuntimeName.Length == 0)
        {
            report.Err("The payload list this app read has no OptiScaler route. Update the app, or try again once the payload carries it.");
        }
        else if (src.Length == 0)
        {
            report.Info("Waiting for the payload folder: OptiScaler, the runtime and the weights.");
        }
        else if (!Directory.Exists(src))
        {
            report.Err($"{src} is not a folder.");
        }
        else
        {
            var missing = (daniel ? pins.OptiFiles.Keys.Append(pins.OptiRuntimeName).Append(WeightsName) : pins.OptiFiles.Keys)
                .Where(n => !File.Exists(Path.Combine(src, n))).ToList();
            if (missing.Count > 0)
                report.Err($"{Joined(missing)} {(missing.Count == 1 ? "is" : "are")} not in the payload folder.");
            else
                report.Ok("Every OptiScaler file is there. Installing verifies the SHA-256 of each one.");
        }
        if (mochizuki) CheckMochizukiPayload(src, pins, report);
        if (wrongGpu) CheckWrongGpu(src, pins, Directory.Exists(dir) && GraphicsDetector.Detect(dir).Width == Route.X86, report);
        if (daniel) CheckHostFg(dir, src, pins, api, report);
        if (daniel) CheckSupplied(ownRuntime, wantedRuntime, pins, Preset.OptiScaler, report);
        if (daniel && HostDlssReason(dir, VersionOf(pins.OptiRuntimeVersion)) is { } why)
            report.Info(UsersHostDlss(dir, InstalledManifest(dir))
                ? $"{why}; OptiScaler hands it the OptiScaler\\nvngx_dlss.dll already here."
                : $"{why}: installing puts NVIDIA's 310 in OptiScaler\\ for it.");

        if (dir.Length == 0)
        {
            report.Info($"Waiting for the {Preset.OptiScaler.FolderLabel().ToLowerInvariant()}.");
            return report;
        }
        if (!Directory.Exists(dir))
        {
            report.Err($"{dir} is not a folder.");
            return report;
        }
        if (!Engine.FolderIsWritable(dir))
            report.Err(
                "That folder cannot be written to. It is either read-only or somewhere that needs "
                + "administrator rights. Run this installer as administrator, or move the game.");

        var detected = GraphicsDetector.Detect(dir);
        var x86 = detected.Width == Route.X86;
        if (x86 && X86Refused(detected, api, pins.OptiScalerVersion) is { } refused)
            report.Err(refused);
        var manifest = InstalledManifest(dir, x86 ? Route.X86 : Route.X64);
        var everywhere = OptiRunsEverywhere(pins.OptiScalerVersion);
        var proxyName = x86 ? OptiX86Proxy(X86Api(detected, api, pins.OptiScalerVersion))
            : OptiProxyFor(proxy, manifest, suggestedProxy, everywhere ? api : null);
        if (x86)
            report.Info($"A 32-bit game: OptiScaler and its 64-bit host go in {OptiHostFolder}\\, and the 32-bit {proxyName} "
                        + "beside the game.");
        else
            NoteProxy(proxyName, proxy, manifest, suggestedProxy, report);
        var retiring = !mochizuki && MochizukiRecorded(manifest).Count > 0;
        var held = new[] { proxyName, WeightsName }.Concat(OptiPasses).Concat(AuthorsRuntimesHere(dir, pins).Select(f => f.Name))
            .Where(n => Engine.IsLocked(Path.Combine(dir, n)))
            .Concat(mochizuki || retiring ? MochizukiHeld(dir) : []).ToList();
        if (held.Count > 0) report.Err(Engine.OpenElsewhere(dir, held));

        if (src.Length > 0 && Directory.Exists(src))
        {
            var need = OptiPayloadSizes(src, pins, daniel)
                .Where(f => Engine.SizeOf(Path.Combine(dir, OptiDestination(f.Name, proxyName))) != f.Size)
                .Aggregate(0UL, (sum, f) => sum + f.Size)
                + (mochizuki ? MochizukiNeed(src, dir, pins) : 0);
            if (Engine.FreeBytes(dir) is { } free && need > 0 && free < need)
                report.Err($"Not enough room: {free / 1_048_576} MB free, and this needs {need / 1_048_576} MB.");
        }

        CheckOptiInTheWay(dir, proxyName, manifest, report, preflight: true);
        CheckForeignMod(dir, manifest, report);
        CheckNeuralHubStub(dir, report);
        CheckRuntimeAsVersionDll(dir, pins, report);
        CheckLocalHip(dir, report);
        CheckRenamedStreamline(dir, report);
        CheckAuthorsRuntime(dir, pins, Preset.OptiScaler, report);
        CheckUpscaler(dir, report, everywhere, GraphicsDetector.Detect(dir).Executable);
        CheckOptiRouteIsReachable(dir, report, everywhere, OptiRunsBridged(pins.OptiScalerVersion),
            OptiRunsX86Wide(pins.OptiScalerVersion));
        if (!mochizuki && manifest?.Entries.Any(e => e.Name == MochizukiRuntimeName && e.Owned) == true)
            report.Info(MochizukiComesOut);
        if (!wrongGpu && WrongGpuRecorded(manifest).Any())
            report.Info("WrongGPU is installed here and is left off this time, so installing takes out what this app put in of it.");

        var homeDir = x86 ? Path.Combine(dir, OptiHostFolder) : dir;
        if (File.Exists(Path.Combine(homeDir, OptiScalerIni))
            && manifest?.Entries.Any(e => e.Name == (x86 ? OptiHostFolder + "/" : "") + OptiScalerIni) != true
            && !ForeignIni(homeDir, ForeignNrMod(dir, manifest).Count > 0))
            report.Info($"{OptiScalerIni} is already here and stays as it is: it is your configuration.");

        if (!report.Failed) report.Ok("Nothing in the way.");
        return report;
    }
}
