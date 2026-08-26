using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;

namespace TextAdvance.Executors;

internal static unsafe class ExecConfirmCutsceneSkip
{
    internal static void Tick()
    {
        var addon = Svc.GameGui.GetAddonByName("SelectString", 1);
        if (addon == IntPtr.Zero) return;
        var selectStrAddon = (AddonSelectString*)addon.Address;
        if (!IsAddonReady(&selectStrAddon->AtkUnitBase))
        {
            return;
        }
        // GetTextNodeById 找不到節點時回 null;這行是內插字串,不管 log 等級都會被求值。
        var titleNode = selectStrAddon->GetTextNodeById(2);
        PluginLog.Debug($"1: {(titleNode == null ? "<null>" : titleNode->NodeText.ToString())}");

        // 🔴 NodeList[3]->GetAsAtkTextNode()->NodeText 是三層裸鏈:版面還沒建好時 NodeList 可能是
        // null 或不足 4 格,而 GetAsAtkTextNode() 是原生呼叫,對空節點一樣會丟出無法攔截的
        // AccessViolationException。任何一層讀不出來就當作「不是跳過過場的選項」,這次不做事。
        var uld = &selectStrAddon->AtkUnitBase.UldManager;
        if (uld->NodeList == null || uld->NodeListCount <= 3) return;
        var optionNode = uld->NodeList[3];
        if (optionNode == null) return;
        var optionTextNode = optionNode->GetAsAtkTextNode();
        if (optionTextNode == null) return;

        if (!Lang.SkipCutsceneStr.Contains(optionTextNode->NodeText.ToString())) return;
        if (EzThrottler.Throttle("SkipCutsceneConfirm"))
        {
            PluginLog.Debug("Selecting cutscene skipping");
            Callback.Fire((AtkUnitBase*)addon.Address, true, 0);
        }
    }
}
