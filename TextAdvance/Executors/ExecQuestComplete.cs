
using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;

namespace TextAdvance.Executors;

internal static unsafe class ExecQuestComplete
{
    internal static void Tick()
    {
        if (TryGetAddonByName<AtkUnitBase>("JournalResult", out var addon) && IsAddonReady(addon))
        {
            // 🔴 同 ExecQuestAccept:節點找不到時 button 是 null,而 IsEnabled 解的是 OwnerNode,
            // 兩者都沒有空指標檢查,直接讀是無法攔截的 AccessViolationException。
            var button = addon->GetComponentButtonById(37);
            if (IsComponentEnabled(button))
            {
                // 守衛放在節流之後、按下之前:「完成」按下即關 JournalResult,同一扇(位址)只按一次 —— 關閉中那幾幀
                // IsAddonReady 與按鈕啟用檢查都仍過,再送 ClickAddonButton 就是攔不到的 AccessViolation。
                if (EzThrottler.Throttle("JournalResultComplete") && AddonPressGuard.TryPressOnce("JournalResult", (nint)addon, "JournalResult.Complete"))
                {
                    PluginLog.Debug("Completing quest");
                    button->ClickAddonButton(addon);
                }
            }
        }
    }
}
