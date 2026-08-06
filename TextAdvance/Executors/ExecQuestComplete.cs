
using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

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
                if (EzThrottler.Throttle("JournalResultComplete"))
                {
                    PluginLog.Debug("Completing quest");
                    button->ClickAddonButton(addon);
                }
            }
        }
    }
}
