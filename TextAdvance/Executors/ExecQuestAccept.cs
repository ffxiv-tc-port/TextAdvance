using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;

namespace TextAdvance.Executors;

internal static unsafe class ExecQuestAccept
{
    internal static void Tick()
    {
        if (TryGetAddonByName<AtkUnitBase>("JournalAccept", out var addon) && IsAddonReady(addon))
        {
            // 🔴 GetComponentButtonById 找不到節點時回 null,而 AtkComponentButton.IsEnabled 解的是
            // OwnerNode(不是 AtkResNode)且完全沒有空指標檢查 —— 直接讀會丟出 AccessViolationException,
            // 那是 corrupted-state exception,try/catch 攔不到。IsComponentEnabled 任一層 null 回 false,
            // 失敗形式退化成「這次不按」,下一次 tick 再試。
            var button = addon->GetComponentButtonById(44);
            if (IsComponentEnabled(button))
            {
                // 守衛放在節流之後、按下之前:「接受」按下即關 JournalAccept,同一扇(位址)只按一次 —— 關閉中那幾幀
                // IsAddonReady 與按鈕啟用檢查都仍過,再送 ClickAddonButton 就是攔不到的 AccessViolation。
                if (EzThrottler.Throttle("JournalAcceptAccept") && AddonPressGuard.TryPressOnce("JournalAccept", (nint)addon, "JournalAccept.Accept"))
                {
                    PluginLog.Debug("Accepting quest");
                    button->ClickAddonButton(addon);
                }
            }
        }
    }
}
