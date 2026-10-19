using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace TextAdvance.Executors;

// 邏輯參考 DailyRoutines 的 AutoUseEventItem:任務目標需要使用任務道具(重要物品)時,在遊戲顯示事件版背包
// (InventoryEventGrid)期間,依「附近進行中任務 → EventItem 表對應道具 → 重要物品欄持有」的鏈路直接對目標使用道具。
// 觸發用 PostSetup(開啟當下)+ PostDraw(可見期間每幀)並以 throttle 節流:API13 的 AddonLifecycle 沒有 Show 事件。
// 刻意不掛 LogMessage hook 壓錯誤訊息／重試:PostDraw 在事件版背包可見期間本身就構成重試迴圈。
public static unsafe class ExecUseEventItem
{
    // 事件版背包的三種介面變體(與 DR 相同)
    private static readonly string[] InventoryEventAddons = ["InventoryEventGrid", "InventoryEventGrid0", "InventoryEventGrid0E"];

    // Quest RowId → 該任務綁定且可使用(有 Action)的 EventItem RowId 清單
    private static Dictionary<uint, List<uint>> QuestToEventItems;

    // 事件版背包每開一次,第一次真的走到任務比對時記一行 Information(只在框架執行緒讀寫)。
    private static bool reportPending;

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
        if (type == AddonEvent.PostSetup) reportPending = true;
        if (!EzThrottler.Throttle("UseEventItemScan", 200)) return;
        TryUseEventItem();
    }

    private static void TryUseEventItem()
    {
        if (!P.IsEnabled() || P.IsDisableButtonHeld() || !C.GetEnableUseEventItem()) return;
        if (Svc.Objects.LocalPlayer == null) return;
        if (Svc.Condition[ConditionFlag.InCombat]) return;
        if (IsCasting()) return;
        // 互動觸發的事件尚未結束:等 PostDraw 下一輪(事件收尾後)再試,對應 DR 的 RunOnTick 重試
        if (Svc.Condition[ConditionFlag.OccupiedInQuestEvent]) return;
        var report = reportPending;
        reportPending = false;
        if (!TryGetNearbyQuest(report, out var questRowId)) return;
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
    private static bool TryGetNearbyQuest(bool report, out uint questRowId)
    {
        questRowId = 0;
        var player = Svc.Objects.LocalPlayer;
        if (player == null) return false;

        // AgentHUD.Instance() 走 CS 的 [Agent] 產生器(agentModule == null ? null : ...),
        // UIModule 尚未建立或 HUD 代理人尚未配置時合法回 null。這支掛在 InventoryEventGrid 的
        // PostDraw 上、每 200ms 跑一次,換區與登入流程都會經過,不能假設代理人一定活著。
        // 解參考 null 的 AccessViolation 是 corrupted-state exception,try/catch 攔不到。
        // fail-closed:取不到 HUD 就當作「附近沒有任務」,這一輪不使用任何任務道具。
        var hud = AgentHUD.Instance();
        if (hud == null) return false;

        var nearbyMarkerNames = new HashSet<string>();
        var sawPayload = false;
        foreach (ref var marker in hud->MapMarkers.AsSpan())
        {
            if (marker.TooltipString == null) continue;
            // 要跟任務名的 ExtractText() 走同一套 Lumina 剝法:ToString() 會把顏色等 payload 位元組原樣留在字串裡,永遠比不中。
            var name = marker.TooltipString->StringPtr.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var distance = Vector3.Distance(player.Position, marker.Position);
            if (marker.Radius <= 1 ? distance <= 5f : distance <= marker.Radius)
            {
                nearbyMarkerNames.Add(name);
                if (report && marker.TooltipString->StringPtr.AsSpan().IndexOf((byte)0x02) >= 0) sawPayload = true;
            }
        }

        string matchedName = null;
        if (nearbyMarkerNames.Count > 0)
        {
            var sheet = Svc.Data.GetExcelSheet<Quest>();
            foreach (ref var quest in QuestManager.Instance()->NormalQuests)
            {
                if (quest.QuestId == 0 || quest.IsHidden) continue;
                var rowId = quest.QuestId + 65536u;
                var questName = sheet.GetRowOrDefault(rowId)?.Name.ExtractText();
                if (questName != null && nearbyMarkerNames.Contains(questName))
                {
                    questRowId = rowId;
                    matchedName = questName;
                    break;
                }
            }
        }

        if (report)
        {
            var result = matchedName != null ? $"比中進行中的任務 {questRowId}「{matchedName}」" : "沒有比中進行中的任務";
            PluginLog.Information($"[UseEventItem] 事件版背包開啟後第一次比對:附近任務標記 {nearbyMarkerNames.Count} 個" +
                                  $"{(sawPayload ? "(標記文字含格式碼)" : "")}「{string.Join("、", nearbyMarkerNames.Take(3))}」,{result}。");
        }
        return matchedName != null;
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
