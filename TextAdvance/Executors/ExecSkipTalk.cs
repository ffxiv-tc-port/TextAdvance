
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
        // 🔴 守衛的 PreFinalize/PostSetup 解除監聽器要在 Click 之前註冊(本 pin 的 RegisterListener 走 RunOnTick FIFO,
        //    依註冊順序被叫到):新的 Talk 重用舊位址時,先解除舊記號、再輪到 Click,才不會白白擋到逃生口。
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
        if (IsEnabled && ((AtkUnitBase*)args.Addon.Address)->IsVisible)
        {
            // 🔴 Talk 是「按一次翻一頁、窗不消失」的多次互動窗,而這個 Click 每幀(PostUpdate)都會進來:
            //    最後一頁按下去窗開始關閉,關閉中那幾幀 IsVisible 仍真,再送一次滑鼠事件就是攔不到的 AccessViolation。
            //    守衛記位址,同一扇 15 幀內只按一次(翻頁節奏 +0.25s,2026-09-02 使用者裁決);被擋 = 這幀不按。
            if (!AddonPressGuard.TryPressOnce("Talk", args.Addon.Address, "Talk.Click", escapeIsRoutine: true)) return;
            new AddonMaster.Talk(args.Addon).Click();
        }
    }
}
