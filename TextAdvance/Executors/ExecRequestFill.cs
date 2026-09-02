using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;
using Callback = ECommons.Automation.Callback;

namespace TextAdvance.Executors;

//by Taurenkey https://github.com/PunishXIV/PandorasBox/blob/24a4352f5b01751767c7ca7f1d4b48369be98711/PandorasBox/Features/UI/AutoSelectTurnin.cs
internal static unsafe class ExecRequestFill
{
    private static bool active = false;

    private static List<int> SlotsFilled { get; set; } = [];

    private static TaskManager TaskManager => P.TaskManager;
    public static bool DontFillThisWindow = false;
    internal static void Tick()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) && IsAddonReady((AtkUnitBase*)addon))
        {
            if (DontFillThisWindow) return;
            for (var i = 1; i <= addon->EntryCount; i++)
            {
                active = true;
                if (SlotsFilled.Contains(addon->EntryCount))
                {
                    P.TaskManager.Abort();
                    return;
                }
                if (SlotsFilled.Contains(i)) return;
                var val = i;
                TaskManager.Enqueue(() => TryClickItem(addon, val));
            }
        }
        else
        {
            DontFillThisWindow = false;
            active = false;
            SlotsFilled.Clear();
            TaskManager.Abort();
        }
    }

    private static bool? TryClickItem(AddonRequest* addon, int i)
    {
        if (SlotsFilled.Contains(i)) return true;

        // 🔴 addon 是 Tick 那一幀捕獲、被 TaskManager 跨 tick 使用的原生指標。任務真正執行時先重查,絕不對捕獲的舊指標解參:
        //    Request 不在(或還沒 ready)→ 這一輪不碰、回 false(Request 消失時 Tick 的 else 分支同一幀已 Abort 整條佇列;
        //    TextAdvance.Tick 先於 TaskManager 訂閱 Framework.Update,所以先被叫到);
        //    在但位址不同(舊窗已換成新窗)→ 這個任務已過期,回 true 讓佇列往下排空,Tick 會用新位址重新排。
        if (!TryGetAddonByName<AddonRequest>("Request", out var live) || !IsAddonReady((AtkUnitBase*)live)) return false;
        if (live != addon) return true;

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu", 1).Address;

        if (contextMenu is null || !contextMenu->IsVisible)
        {
            var slot = i - 1;

            // 刻意的重試迴圈(每 tick 重送直到 ContextIconMenu 出現)也要罩:同一扇 Request(位址)的同一格
            // 15 幀內不重送(選單通常 1~2 幀就出現,正常路徑不受影響);交出之後(不帶參數的 "Request" key
            // 記著這個位址)任何一格都不送 —— 使用者中途取消、窗關閉中那幾幀 IsAddonReady 仍全過,再 Fire 就是 AccessViolation。
            // 被擋回 false = 與「送了但選單還沒出現」同一條既有路徑,下一 tick 再來;不回 null(null 會清掉整條佇列)。
            if (!AddonPressGuard.TryPressOnce("Request", (nint)addon, "Request.Fill", paramKey: $"fill:{slot}", escapeIsRoutine: true)) return false;
            Callback.Fire(&addon->AtkUnitBase, false, 2, slot, 0, 0);

            return false;
        }
        else
        {
            var contextIconMenu = (AddonContextIconMenu*)contextMenu;
            var entryCount = contextIconMenu->EntryCount;

            // Determine which option to select based on quality preference
            var qualityPref = C.GetRequestFillQualityPreference();
            int selectedIndex = 0; // Default to first option

            if (entryCount > 1 && qualityPref == RequestFillQualityPreference.HQ)
            {
                // When both NQ and HQ exist, game lists HQ first (index 0), NQ second (index 1)
                selectedIndex = 0; // Select HQ
                PluginLog.Debug($"Slot {i}: {entryCount} qualities, selecting HQ (index {selectedIndex})");
            }
            else if (entryCount > 1 && qualityPref == RequestFillQualityPreference.NQ)
            {
                selectedIndex = 1; // Select NQ (second option when both available)
                PluginLog.Debug($"Slot {i}: {entryCount} qualities, selecting NQ (index {selectedIndex})");
            }
            else
            {
                // Any quality or only one option available - use first
                selectedIndex = 0;
                PluginLog.Debug($"Slot {i}: Using first available option (index {selectedIndex})");
            }

            // Fire callback to select item from context menu.
            // 🔴 選件即關 ContextIconMenu:同一扇(位址)只送一次。上一格選完、選單關閉中的那幾幀 IsVisible 仍真,
            //    下一格的任務(同一批 Enqueue 的)會走到這裡 —— 被擋回 false(不是 null:null 會清掉整條佇列),
            //    也不把這格記成已填;等選單真的收掉(位址消失/PreFinalize)後再對 Request 重新開一次選單。
            if (!AddonPressGuard.TryPressOnce("ContextIconMenu", (nint)contextMenu, "ContextIconMenu.Select")) return false;
            Callback.Fire(contextMenu, false, 0, selectedIndex, 1021003, 0, 0);
            SlotsFilled.Add(i);
            return true;
        }
    }

    internal static List<uint> GetRequestedItemList()
    {
        var ret = new List<uint>();
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) && IsAddonReady((AtkUnitBase*)addon))
        {
            var invman = InventoryManager.Instance();
        }
        return ret;
    }
}
