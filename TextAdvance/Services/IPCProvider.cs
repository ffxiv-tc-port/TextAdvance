using ECommons.EzIpcManager;
using System.Collections.Concurrent;
using TextAdvance.Navmesh;

namespace TextAdvance.Services;
public class IPCProvider
{
    public ExternalTerritoryConfig ExternalConfig = null;
    public string Requester = null;

    private IPCProvider()
    {
        EzIPC.Init(this);
    }

    /// <summary>
    /// 把端點的實際工作放到 framework 執行緒上跑。
    /// <br/><br/>
    /// 🔴 IPC 端點跑在<b>呼叫端的執行緒</b>上。下面這些端點會走到
    /// <c>Player.Object.Position</c>(<c>IObjectTable</c> 的包裝是每格重用、Address 就地改寫的,
    /// 從別的執行緒讀等於對隨時可能被換掉的原生指標解參考)、
    /// 會對 <c>TaskManager</c> 的兩個裸 <c>List</c> 做 Enqueue/Abort(framework 執行緒同時在
    /// 走訪它們),還會往 vnavmesh 打 IPC —— 三種都不能在別人的執行緒上做。
    /// <br/><br/>
    /// 已經在 framework 執行緒時<b>就地執行</b>:例外照樣往呼叫端擲,回傳值與時序與改動前
    /// 完全相同(Questionable、AutoDuty 這些從自己的 framework tick 打進來的呼叫走這條)。
    /// 在別的執行緒時排到下一次 Framework.Update 且<b>不等待</b> —— 這幾個端點回傳型別都是
    /// <c>void</c>,「不等待」不改變任何回傳語意,只是把生效時間往後挪最多一幀。
    /// 不等待也避免了「呼叫端持著鎖同步等 framework 執行緒」這種死結形狀。
    /// </summary>
    private static void RunOnFramework(string endpointName, Action action)
    {
        if (Svc.Framework.IsInFrameworkUpdateThread)
        {
            action();
            return;
        }
        PendingWork.Enqueue((endpointName, action));
    }

    /// <summary>
    /// 從別的執行緒進來的端點工作,照先進先出排在這裡等 framework 執行緒來排乾。
    /// </summary>
    /// <remarks>
    /// 🔴 這裡刻意<b>不</b>用 <c>Svc.Framework.RunOnFrameworkThread</c> 逐則排隊:本 pin 的
    /// <c>ThreadBoundTaskScheduler</c> 把待跑的工作放在 <c>ConcurrentDictionary</c> 裡、
    /// <c>Run()</c> 走訪的是 <c>Keys</c>(Dalamud/Utility/ThreadBoundTaskScheduler.cs) ——
    /// <b>同一格內不保證先進先出</b>。而這幾個端點的順序是有語意的:
    /// 呼叫端連著打 <c>Stop()</c> 再 <c>EnqueueMoveTo3DPoint()</c>,順序一倒過來就變成
    /// 「先排好移動、再把它整個中止」,失敗形式是「叫它走它不走」而且完全不報錯。
    /// </remarks>
    private static readonly ConcurrentQueue<(string Name, Action Action)> PendingWork = new();

    /// <summary>
    /// 由 <c>TextAdvance.Tick</c>(framework 執行緒)每幀呼叫一次,把 <see cref="PendingWork"/> 排乾。
    /// 每一則各自包 try:其中一則擲例外不會讓後面的排不出去,也不會中斷 Tick 的其餘部分。
    /// </summary>
    internal static void DrainPendingWork()
    {
        while (PendingWork.TryDequeue(out var work))
        {
            try
            {
                work.Action();
            }
            catch (Exception e)
            {
                PluginLog.Error($"[TextAdvance] IPC {work.Name} 在 framework 執行緒上執行失敗:{e}");
            }
        }
    }

    [EzIPC]
    public bool EnableExternalControl(string requester, ExternalTerritoryConfig config)
    {
        if (!this.IsInExternalControl() || this.Requester == requester)
        {
            this.ExternalConfig = config;
            this.Requester = requester;
            return true;
        }
        return false;
    }

    [EzIPC]
    public bool DisableExternalControl(string requester)
    {
        if (!this.IsInExternalControl() || this.Requester == requester)
        {
            this.ExternalConfig = null;
            this.Requester = null;
            return true;
        }
        return false;
    }

    [EzIPC]
    public bool IsInExternalControl()
    {
        return this.Requester != null && this.ExternalConfig != null;
    }

    /// <summary>
    /// 🔴 這個端點跑在<b>呼叫端的執行緒</b>上(CallGate 不做 marshal),而 Questionable 與
    /// AutoDuty 會高頻查詢它。<c>P.IsEnabled()</c> 會走到 <c>IsEnableButtonHeld()</c>,
    /// 那裡讀 <c>ImGui.GetIO()</c>(解參考 imgui 的全域 context)與
    /// <c>CSFramework.Instance()-&gt;WindowInactive</c>(原生靜態指標),兩個都不是可以從
    /// 別的執行緒碰的東西 —— 而 AccessViolation 在 .NET Core 是 corrupted-state exception,
    /// try/catch 完全攔不到。
    /// <br/><br/>
    /// 這裡刻意<b>不</b>用 RunOnFrameworkThread 同步等待:高頻布林查詢等一幀會把呼叫端的
    /// 執行緒卡到下一次 Framework.Update。改成讀 framework 執行緒每幀更新的
    /// <c>TextAdvance.IsEnabledPureSnapshot</c>,最舊差一幀。
    /// 呼叫端本來就在 framework 執行緒上時(絕大多數情況)走原路,回傳值與時序不變。
    /// </summary>
    [EzIPC]
    public bool IsEnabled() => Svc.Framework.IsInFrameworkUpdateThread ? P.IsEnabled(true) : P.IsEnabledPureSnapshot;
    [EzIPC] public bool GetEnableQuestAccept() => C.GetEnableQuestAccept();
    [EzIPC] public bool GetEnableQuestComplete() => C.GetEnableQuestComplete();
    [EzIPC] public bool GetEnableRewardPick() => C.GetEnableRewardPick();
    [EzIPC] public bool GetEnableCutsceneEsc() => C.GetEnableCutsceneEsc();
    [EzIPC] public bool GetEnableCutsceneSkipConfirm() => C.GetEnableCutsceneSkipConfirm();
    [EzIPC] public bool GetEnableRequestHandin() => C.GetEnableRequestHandin();
    [EzIPC] public bool GetEnableRequestFill() => C.GetEnableRequestFill();
    [EzIPC] public RequestFillQualityPreference GetRequestFillQualityPreference() => C.GetRequestFillQualityPreference();
    [EzIPC] public bool GetEnableTalkSkip() => C.GetEnableTalkSkip();
    [EzIPC] public bool GetEnableAutoInteract() => C.GetEnableAutoInteract();
    [EzIPC] public bool IsPaused() => P.BlockList.Count != 0;

    [EzIPC]
    public void EnqueueMoveAndInteract(MoveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        RunOnFramework(nameof(EnqueueMoveAndInteract), () => S.MoveManager.EnqueueMoveAndInteract(data, 3f));
    }

    [EzIPC]
    public void EnqueueMoveTo2DPoint(MoveData data, float distance)
    {
        ArgumentNullException.ThrowIfNull(data);
        RunOnFramework(nameof(EnqueueMoveTo2DPoint), () => S.MoveManager.MoveTo2DPoint(data, distance));
    }

    [EzIPC]
    public void EnqueueMoveTo3DPoint(MoveData data, float distance)
    {
        ArgumentNullException.ThrowIfNull(data);
        RunOnFramework(nameof(EnqueueMoveTo3DPoint), () => S.MoveManager.MoveTo3DPoint(data, distance));
    }

    [EzIPC]
    public void Stop()
    {
        RunOnFramework(nameof(Stop), () =>
        {
            S.EntityOverlay.TaskManager.Abort();
            if (C.Navmesh) P.NavmeshManager.Stop();
        });
    }
    [EzIPC]
    public bool IsBusy()
    {
        return S.EntityOverlay.TaskManager.IsBusy;
    }
}
