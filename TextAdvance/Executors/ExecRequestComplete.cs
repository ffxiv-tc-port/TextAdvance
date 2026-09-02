using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;

namespace TextAdvance.Executors;

internal static unsafe class ExecRequestComplete
{
    private static long RequestAllow = 0;
    internal static void Tick()
    {
        if (TryGetAddonByName<AtkUnitBase>("Request", out var addon) && IsAddonReady(addon))
        {
            // 🔴 這個「開窗後先等 4 幀」原本數的是 UiBuilder.FrameCount,而那個計數器在外掛 UI 被隱藏時
            //    完全停止前進(Dalamud 的 ToggleUiHideDuringCutscenes 預設是開的 ⇒ 過場動畫中必定凍結),
            //    於是這 4 幀永遠等不完、交納永不送出。改用 AddonPressGuard 自己掛在原生 Framework::Tick
            //    上的時鐘,不經過繪製路徑,UI 隱不隱藏都照跑。理由詳見 AddonPressGuard.CurrentFrame。
            //    📌 RequestAllow 的 0 仍可安全當「還沒開始等」的哨兵:CurrentFrame 單調不減且非負,
            //    CurrentFrame + 4 至少是 4。
            if (RequestAllow == 0)
            {
                RequestAllow = AddonPressGuard.CurrentFrame + 4;
            }
            if (AddonPressGuard.CurrentFrame < RequestAllow) return;
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
