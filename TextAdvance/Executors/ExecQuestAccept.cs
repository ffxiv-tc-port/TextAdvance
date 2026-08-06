using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

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
                if (EzThrottler.Throttle("JournalAcceptAccept"))
                {
                    PluginLog.Debug("Accepting quest");
                    button->ClickAddonButton(addon);
                }
            }
        }
    }
}
