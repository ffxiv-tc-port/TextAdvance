using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace TextAdvance.Executors;

// 移植自 DailyRoutines 的 AutoUseEventItem(API15/CN 框架,僅參考邏輯後以 API13 重寫):
// 當任務目標需要使用任務道具(重要物品)時,在遊戲顯示事件版背包(InventoryEventGrid)期間,
// 依「附近進行中任務 → EventItem 表對應道具 → 重要物品欄持有」的鏈路直接對目標使用道具。
// 與 DR 的差異(API13 落差):
// - DR 用 AddonEvent.PreShow 觸發;API13 的 AddonLifecycle 沒有 Show 事件,改用
//   PostSetup(開啟當下)+ PostDraw(可見期間每幀)並以 throttle 節流。
// - DR 掛 LogMessage hook 壓錯誤訊息並在 579(當前狀態無法使用)時重試;這裡不掛 hook,
//   PostDraw 在事件版背包可見期間本身就構成重試迴圈(事件佔用解除後的下一輪就會用出去)。
public static unsafe class ExecUseEventItem
{
    // 事件版背包的三種介面變體(與 DR 相同)
    private static readonly string[] InventoryEventAddons = ["InventoryEventGrid", "InventoryEventGrid0", "InventoryEventGrid0E"];

    // Quest RowId → 該任務綁定且可使用(有 Action)的 EventItem RowId 清單
    private static Dictionary<uint, List<uint>> QuestToEventItems;

    public static void Init()
    {
        QuestToEventItems = Svc.Data.GetExcelSheet<EventItem>()
            .Where(x => x.Quest.RowId > 0 && x.Action.RowId > 0)
            .GroupBy(x => x.Quest.RowId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RowId).ToList());
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, InventoryEventAddons, OnAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, InventoryEventAddons, OnAddon);
    }

    public static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(OnAddon);
    }

    private static void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (!EzThrottler.Throttle("UseEventItemScan", 200)) return;
        TryUseEventItem();
    }

    private static void TryUseEventItem()
    {
        if (!P.IsEnabled() || P.IsDisableButtonHeld() || !C.GetEnableUseEventItem()) return;
        if (Svc.ClientState.LocalPlayer == null) return;
        if (Svc.Condition[ConditionFlag.InCombat]) return;
        if (IsCasting()) return;
        // 互動觸發的事件尚未結束:等 PostDraw 下一輪(事件收尾後)再試,對應 DR 的 RunOnTick 重試
        if (Svc.Condition[ConditionFlag.OccupiedInQuestEvent]) return;
        if (!TryGetNearbyQuest(out var questRowId)) return;
        if (!QuestToEventItems.TryGetValue(questRowId, out var eventItems)) return;

        var target = Svc.Targets.Target ?? Svc.Objects.FirstOrDefault(x => x.IsMTQ());
        if (target == null) return;

        var usable = FilterByKeyItemInventory(eventItems);
        if (usable.Count == 0) return;
        if (!EzThrottler.Throttle("UseEventItem", 500)) return;

        foreach (var itemId in usable)
        {
            if (IsCasting()) return;
            var pos = target.Position;
            ActionManager.Instance()->UseActionLocation(ActionType.EventItem, itemId, target.GameObjectId, &pos);
        }
    }

    private static bool IsCasting()
        => Svc.Condition[ConditionFlag.Casting] || Svc.Condition[ConditionFlag.Casting87];

    // DR 的判定:HUD 導航標記(tooltip = 任務名)在半徑內,且該任務在進行中清單裡
    private static bool TryGetNearbyQuest(out uint questRowId)
    {
        questRowId = 0;
        var player = Svc.ClientState.LocalPlayer;
        if (player == null) return false;

        var nearbyMarkerNames = new HashSet<string>();
        foreach (ref var marker in AgentHUD.Instance()->MapMarkers.AsSpan())
        {
            if (marker.TooltipString == null) continue;
            var name = marker.TooltipString->ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var distance = Vector3.Distance(player.Position, marker.Position);
            if (marker.Radius <= 1 ? distance <= 5f : distance <= marker.Radius)
            {
                nearbyMarkerNames.Add(name);
            }
        }
        if (nearbyMarkerNames.Count == 0) return false;

        var sheet = Svc.Data.GetExcelSheet<Quest>();
        foreach (ref var quest in QuestManager.Instance()->NormalQuests)
        {
            if (quest.QuestId == 0 || quest.IsHidden) continue;
            var rowId = quest.QuestId + 65536u;
            var questName = sheet.GetRowOrDefault(rowId)?.Name.ExtractText();
            if (questName != null && nearbyMarkerNames.Contains(questName))
            {
                questRowId = rowId;
                return true;
            }
        }
        return false;
    }

    private static List<uint> FilterByKeyItemInventory(List<uint> validItems)
    {
        var ret = new List<uint>();
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.KeyItems);
        if (container == null || !container->IsLoaded) return ret;
        for (var i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot == null || slot->ItemId == 0) continue;
            if (!validItems.Contains(slot->ItemId)) continue;
            ret.Add(slot->ItemId);
        }
        return ret;
    }
}
