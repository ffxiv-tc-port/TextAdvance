using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;

namespace TextAdvance.Executors;

internal static unsafe class ExecRequestComplete
{
    private static ulong RequestAllow = 0;
    internal static void Tick()
    {
        if (TryGetAddonByName<AtkUnitBase>("Request", out var addon) && IsAddonReady(addon))
        {
            if (RequestAllow == 0)
            {
                RequestAllow = Svc.PluginInterface.UiBuilder.FrameCount + 4;
            }
            if (Svc.PluginInterface.UiBuilder.FrameCount < RequestAllow) return;
            var m = new AddonMaster.Request(addon);
            if (m.IsHandOverEnabled && m.IsFilled)
            {
                // 守衛放在節流之後、按下之前:交出即關 Request,同一扇(位址)只按一次 —— 關閉中那幾幀
                // IsAddonReady 三關全過、IsHandOverEnabled 也可能仍真,再送 ClickAddonButton 就是攔不到的 AccessViolation。
                // 與 ExecRequestFill 共用窗名 "Request":交出之後那扇窗的任何逐格填入也一併擋到它收掉。
                if (EzThrottler.Throttle("Handin") && AddonPressGuard.TryPressOnce("Request", (nint)addon, "Request.HandOver"))
                {
                    PluginLog.Debug("Handing over request");
                    m.HandOver();
                }
            }
        }
        else
        {
            RequestAllow = 0;
        }
    }
}
