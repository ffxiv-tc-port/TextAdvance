using ECommons.Automation;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;

namespace TextAdvance.Executors;

internal static unsafe class ExecConfirmCutsceneSkip
{
    // 台服退路只記一次,避免每幀洗 log。
    private static bool LoggedYesFallback;

    internal static void Tick()
    {
        // 上游 ab04799 把寫死的 NodeList[3] 索引換成 ECommons 的 AddonMaster.SelectString,
        // 但那一版單獨拿來用是壞的:它拿「選項文字」去比對 Lang.SkipCutsceneStr,
        // 而 SkipCutsceneStr 裝的是對話框的「提示文字」(SelectString.uld 的 Node 2;
        // 台服 Addon#281「要跳過這段過場動畫嗎？」),選項只有「是」/「否」,永遠比不中。
        // 上游自己在 06b4995 修正成「提示比 SkipCutsceneStr、選項比 YesStr」,這裡直接採用修正後的版本。
        if(!TryGetAddonMaster<AddonMaster.SelectString>(out var m) || !m.IsAddonReady) return;

        // 🔴 這一關是「這個 SelectString 到底是不是跳過過場的確認框」的身分證明。
        // m.Text 讀的是 Node 2 的提示文字(取不到時 ECommons 回空字串,不會崩)。
        // 比對不中就完全不動作 —— 身分沒確認之前絕不能在別人的選單上按東西。
        if(!m.Text.ContainsAny(StringComparison.OrdinalIgnoreCase, Lang.SkipCutsceneStr)) return;

        var entries = m.Entries;
        if(entries.Length == 0) return;

        foreach(var x in entries)
        {
            if(Lang.YesStr.Contains(x.Text))
            {
                if(EzThrottler.Throttle("SkipCutsceneConfirm"))
                {
                    x.Select();
                }
                return;
            }
        }

        // 🔴 台服退路:Lang.YesStr 在這次改動之前是死碼(整個外掛沒有任何呼叫點),
        // 從沒在台服實機跑過。離線比對台服 Addon 表裡的 38 組 yes/no 全都是「是」/「否」,
        // 沒有「是。」之類的變體,所以上面那圈照理會命中;萬一沒中,退回改動前的行為
        // (Callback.Fire(addon, true, 0) = 選第 0 項)。這不是新增的風險 ——
        // 身分已經由提示文字證明過,而第 0 項本來就是這個外掛長年在按的那一項。
        if(!LoggedYesFallback)
        {
            LoggedYesFallback = true;
            var texts = new List<string>();
            foreach(var x in entries) texts.Add(x.Text);
            PluginLog.Information($"[TextAdvance] 跳過過場確認框:選項文字都不在 Lang.YesStr 內,退回選第 0 項。實際選項=[{string.Join(" | ", texts)}]");
        }
        if(EzThrottler.Throttle("SkipCutsceneConfirm"))
        {
            entries[0].Select();
        }
    }
}
