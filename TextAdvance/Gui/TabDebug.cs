using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextAdvance.Executors;
using Callback = ECommons.Automation.Callback;

namespace TextAdvance.Gui;

internal static unsafe class TabDebug
{
    private static TaskManager TestTaskManager;
    internal static void Draw()
    {
        if (ImGui.CollapsingHeader("IPC test"))
        {
            ImGuiEx.Text($"""
                IsEnabled {S.IPCTester.IsEnabled()}
                GetEnableQuestAccept {S.IPCTester.GetEnableQuestAccept()}
                GetEnableQuestComplete {S.IPCTester.GetEnableQuestComplete()}
                GetEnableRewardPick {S.IPCTester.GetEnableRewardPick()}
                GetEnableCutsceneEsc {S.IPCTester.GetEnableCutsceneEsc()}
                GetEnableCutsceneSkipConfirm {S.IPCTester.GetEnableCutsceneSkipConfirm()}
                GetEnableRequestHandin {S.IPCTester.GetEnableRequestHandin()}
                GetEnableRequestFill {S.IPCTester.GetEnableRequestFill()}
                GetEnableTalkSkip {S.IPCTester.GetEnableTalkSkip()}
                GetEnableAutoInteract {S.IPCTester.GetEnableAutoInteract()}
                IsPaused {S.IPCTester.IsPaused()}
                """);
        }
        if (ImGui.CollapsingHeader("External control test"))
        {
            var opts = Ref<ExternalTerritoryConfig>.Get("", () => new());
            ImGuiEx.Checkbox("EnableAutoInteract", ref opts.EnableAutoInteract);
            ImGuiEx.Checkbox("EnableCutsceneEsc", ref opts.EnableCutsceneEsc);
            ImGuiEx.Checkbox("EnableCutsceneSkipConfirm", ref opts.EnableCutsceneSkipConfirm);
            ImGuiEx.Checkbox("EnableQuestAccept", ref opts.EnableQuestAccept);
            ImGuiEx.Checkbox("EnableQuestComplete", ref opts.EnableQuestComplete);
            ImGuiEx.Checkbox("EnableRequestFill", ref opts.EnableRequestFill);
            ImGuiEx.Checkbox("EnableRequestHandin", ref opts.EnableRequestHandin);
            ImGuiEx.Checkbox("EnableRewardPick", ref opts.EnableRewardPick);
            ImGuiEx.Checkbox("EnableTalkSkip", ref opts.EnableTalkSkip);
            ImGuiEx.Text($"Is in external control: {S.IPCTester.IsInExternalControl()}");
            if (ImGui.Button("Enable external control (Plugin1)")) DuoLog.Information(S.IPCTester.EnableExternalControl("Plugin1", opts).ToString());
            if (ImGui.Button("Enable external control (Plugin2)")) DuoLog.Information(S.IPCTester.EnableExternalControl("Plugin2", opts).ToString());
            if (ImGui.Button("Disable external control (Plugin1)")) DuoLog.Information(S.IPCTester.DisableExternalControl("Plugin1").ToString());
            if (ImGui.Button("Disable external control (Plugin2)")) DuoLog.Information(S.IPCTester.DisableExternalControl("Plugin2").ToString());
        }
        if (ImGui.CollapsingHeader("Cutscene"))
        {

        }
        if (ImGui.CollapsingHeader("Request"))
        {
            if (TryGetAddonByName<AddonRequest>("Request", out var request) && IsAddonReady((AtkUnitBase*)request))
            {
                ImGuiEx.Text($"{request->EntryCount}");
            }
        }
        if (ImGui.Button("Install hook")) Callback.InstallHook();
        if (ImGui.Button("UnInstall hook")) Callback.UninstallHook();
        if (ImGui.CollapsingHeader("Antistuck"))
        {
            ImGuiEx.Text($"""
                Last position: {S.MoveManager.LastPosition}
                Last update: {S.MoveManager.LastPositionUpdate} ({Environment.TickCount64 - S.MoveManager.LastPositionUpdate} ms ago)
                IsRunning: {P.NavmeshManager.IsRunning()}
                Animation locked: {Player.IsAnimationLocked} / {Player.AnimationLock}
                """);
        }
        if (ImGui.CollapsingHeader("Quest markers"))
        {
            // 這個除錯分頁在角色選擇畫面也畫得出來,而 AgentHUD.Instance() 走 CS 的
            // [Agent] 產生器(agentModule == null ? null : ...),那時候合法回 null。
            // 「不知道」要在畫面上看得見,不要把它畫成空清單。
            var hud = AgentHUD.Instance();
            if (hud == null)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "AgentHUD unavailable (?)");
            }
            else
            {
                var markers = hud->MapMarkers.AsSpan();
                for (var i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (ThreadLoadImageHandler.TryGetIconTextureWrap(marker.IconId, false, out var tex))
                    {
                        ImGui.Image(tex.Handle, tex.Size);
                    }
                    ImGuiEx.Text($"{marker.IconId} / {marker.Position} / {Vector3.Distance(Player.Position, marker.Position)}");
                    ImGui.Separator();
                }
            }
        }
        if (ImGui.Button("copy target descriptor"))
        {
            if (Svc.Targets.Target != null) Copy(new ObjectDescriptor(Svc.Targets.Target, true).AsCtorString());
        }
        if (ImGui.CollapsingHeader("Auto interact"))
        {
            ImGuiEx.Text($"Target: {ExecAutoInteract.WasInteracted(Svc.Targets.Target)}");
            ImGuiEx.Text($"Auto interacted objects: {ExecAutoInteract.InteractedObjects.Print("\n")}");
        }
        if (ImGui.CollapsingHeader("Quests"))
        {
            ImGuiEx.Text($"{Utils.GetQuestArray().Print("\n")}");
        }
        if (ImGui.CollapsingHeader("Reward pick"))
        {
            // 同 ExecPickReward:NodeList[7] 不能裸解參考,空指標是攔不到的 AccessViolationException。
            if (TryGetAddonByName<AtkUnitBase>("JournalResult", out var addon) && IsAddonReady(addon)
                && addon->UldManager.NodeList != null && addon->UldManager.NodeListCount > 7
                && addon->UldManager.NodeList[7] != null)
            {
                var canvas = addon->UldManager.NodeList[7];
                var r = new ReaderJournalResult(addon);
                ImGuiEx.Text($"Rewards: \n{r.OptionalRewards.Select(x => $"ID:{x.ItemID} / Icon:{x.IconID} / Amount:{x.Amount} / Name:{x.Name} ").Print("\n")}");
                for (var i = 0; i < 5; i++)
                {
                    if (ImGui.Button($"{i}"))
                    {
                        S.Memory.PickRewardItemUnsafe((nint)canvas->GetComponent(), i);
                    }
                }
                if (ImGui.Button("Stress test"))
                {
                    TestTaskManager ??= new();
                    // 🔴 canvas 是「這一幀」的原生節點指標,而這裡一次排 1000 個任務、每 tick 只跑一個,
                    //    最後一個要等約 1000 幀(約 16 秒)才輪到。JournalResult 早就關掉了——
                    //    對已釋放的節點呼叫 GetComponent() 是 AccessViolationException,
                    //    在 .NET Core 屬 corrupted-state exception,try/catch 與 HookSafety 都攔不到。
                    //    (PickRewardItemUnsafe 的 canvas < 1024 防護只擋得住空值,擋不住「非空但已懸空」。)
                    //    正解同 ExecRequestFill.TryClickItem:只抄走窗的位址做等值比較,
                    //    節點在任務真正執行的那一幀重查、重驗、重新取得。
                    var expectedAddon = (nint)addon;
                    for (var i = 0; i < 1000; i++)
                    {
                        var x = i % 5;
                        TestTaskManager.Enqueue(() => StressPickReward(expectedAddon, x));
                    }
                }
                if (TestTaskManager != null)
                {
                    ImGuiEx.Text($"Task {TestTaskManager.MaxTasks - TestTaskManager.NumQueuedTasks}/{TestTaskManager.MaxTasks}");
                }
                if (ImGui.Button("Stop stress test"))
                {
                    TestTaskManager.Abort();
                }
            }
        }
    }

    /// <summary>
    /// 壓力測試用的單發。不接受任何「排入那一幀捕獲的原生指標」:
    /// <paramref name="expectedAddon"/> 只做等值比較、永不解參考,窗換成另一扇時代表這批任務已過期。
    /// 節點與 Component 一律在執行的當下重查重驗,對應 <c>ExecPickReward.OnJournalResultSetup</c> 的判空階梯。
    /// 刻意回傳 void 以維持原本的 <c>Enqueue(Action)</c> 多載語意(每個任務跑一次就出佇列,共 1000 次);
    /// 改成 Func&lt;bool?&gt; 回 false 會讓第一個任務永遠重試,那是行為回退。
    /// </summary>
    private static void StressPickReward(nint expectedAddon, int index)
    {
        if (!TryGetAddonByName<AtkUnitBase>("JournalResult", out var live) || !IsAddonReady(live)) return;
        if ((nint)live != expectedAddon) return;
        if (live->UldManager.NodeList == null || live->UldManager.NodeListCount <= 7) return;
        var node = live->UldManager.NodeList[7];
        if (node == null) return;
        var component = node->GetComponent();
        if (component == null) return;
        S.Memory.PickRewardItemUnsafe((nint)component, index);
    }
}
