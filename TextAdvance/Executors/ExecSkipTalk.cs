
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Helpers;

namespace TextAdvance.Executors;

internal static unsafe class ExecSkipTalk
{
    internal static bool IsEnabled = false;

    internal static void Init()
    {
        // 🔴 守衛的 PreFinalize/PostSetup 解除監聽器先於 Click 註冊;但本 pin 的 RegisterListener 走 RunOnTick、不保序,
        //    註冊順序不是呼叫順序的保證 —— 真正把順序這個變數拿掉的是 AddonPressGuard.OnLifecycle 的同幀豁免。
        //    Click 掛在 PostUpdate 上,addon 不存在的幀不會被叫到,所以解除點不能只靠輪詢。
        AddonPressGuard.Watch("Talk");
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    private static void Click(AddonEvent type, AddonArgs args)
    {
        var atk = (AtkUnitBase*)args.Addon.Address;
        if (IsEnabled && atk->IsVisible)
        {
            // 🔴 Talk 是「按一次翻一頁、窗不消失」的多次互動窗,而這個 Click 每幀(PostUpdate)都會進來:
            //    最後一頁按下去窗開始關閉,關閉中那幾幀 IsVisible 仍真,再送一次滑鼠事件就是攔不到的 AccessViolation。
            //    守衛記位址,同一扇 15 幀內只按一次(翻頁節奏 +0.25s,使用者裁決過的取捨);被擋 = 這幀不按。
            //    文字指紋讓守衛分得出「翻到下一頁了」與「按了但什麼都沒變」,後者才會進退避。
            //    「一個字都沒有」只記 log 不擋補按:那個前提沒有離線證據,假設反了會讓第 2 頁起不再翻頁。
            if (!HasAnyText(atk, out var token)) AddonPressGuard.LogAddonHasNoText("Talk", args.Addon.Address, "Talk.Click");
            if (!AddonPressGuard.TryPressOnce("Talk", args.Addon.Address, "Talk.Click", escapeIsRoutine: true, contentToken: token)) return;
            new AddonMaster.Talk(args.Addon).Click();
        }
    }

    /// <summary>這扇 Talk 現在有沒有字,順便把顯示中的文字壓成一個指紋。</summary>
    /// <remarks>
    /// 只在這扇 addon 自己的 PostSetup/PostUpdate 裡呼叫,節點必然還活著。
    /// 刻意只讀原始位元組不解碼:解碼會把 item link 之類的 payload 變成 U+FFFD,「有沒有字」就會被讀錯。
    /// 讀不到節點清單時回 <see langword="true"/>(當作有字,不會誤記 log);布林值<b>只拿去記 log</b>,不影響按不按。
    /// </remarks>
    /// <returns><see langword="false"/> 只在確定讀到了文字節點、而且一個位元組都沒有時回。</returns>
    private static bool HasAnyText(AtkUnitBase* atk, out int token)
    {
        token = 0;
        if (atk->UldManager.LoadedState != AtkLoadState.Loaded) return true;
        var list = atk->UldManager.NodeList;
        var count = atk->UldManager.NodeListCount;
        if (list == null || count == 0) return true;
        var hash = unchecked((int)2166136261);
        var seen = false;
        var bytes = 0;
        for (var i = 0; i < count; i++)
        {
            var node = list[i];
            if (node == null || node->Type != NodeType.Text) continue;
            seen = true;
            var span = ((AtkTextNode*)node)->NodeText.AsSpan();
            bytes += span.Length;
            foreach (var b in span) hash = unchecked((hash ^ b) * 16777619);
        }
        if (!seen) return true;
        // 0 對守衛的意思是「沒有指紋」,所以雜湊剛好算出 0 時挪開一格。
        token = hash == 0 ? 1 : hash;
        return bytes > 0;
    }
}
