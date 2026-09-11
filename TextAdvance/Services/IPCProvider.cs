using ECommons.EzIpcManager;
using System.Collections.Concurrent;
using TextAdvance.Navmesh;

namespace TextAdvance.Services;
public class IPCProvider
{
    /// <summary>
    /// 外部控制的整份狀態。<b>不可變</b>,而且永遠整份換掉,絕不就地改欄位。
    /// </summary>
    /// <remarks>
    /// 🔴 這裡原本是兩個各自獨立的公開欄位(<c>ExternalConfig</c> 與 <c>Requester</c>)。
    /// 寫入端是 <c>EnableExternalControl</c>／<c>DisableExternalControl</c> 兩支 IPC 端點,
    /// 跑在<b>呼叫端的執行緒</b>上;讀取端是 framework 執行緒每幀走的
    /// <c>Config.GetEnableXXX()</c>(11 處)、繪製執行緒的 <c>TabConfig</c>、以及 <c>Overlay</c>。
    /// 兩個欄位分兩次寫 ⇒ 讀端看得到「<c>Requester</c> 已清空但 <c>ExternalConfig</c> 還在」
    /// 或反過來的中間態。舊碼先問 <c>IsInExternalControl()</c>(讀兩個欄位)再<b>另外一次</b>
    /// 讀 <c>ExternalConfig</c> 解參考,兩次讀之間被清空就是 NullReferenceException。
    /// ⇒ 合成單一參照之後,讀端一次 <c>Volatile.Read</c> 就拿到整份,中間態不存在。
    /// <br/><br/>
    /// ⚠️ 不可變的是這一對<b>參照</b>。<c>Config</c> 指到的 <c>ExternalTerritoryConfig</c>
    /// 是呼叫端交過來的物件,呼叫端事後自己改它的欄位我們管不到 —— 那是既有契約,不在本次範圍。
    /// </remarks>
    public sealed record ExternalControlState(string Requester, ExternalTerritoryConfig Config);

    private ExternalControlState CurrentExternalControl = null;

    /// <summary>
    /// 寫入端互斥用。三個寫入點都是「先看現況再決定寫不寫」,不互斥時兩個外掛同時打
    /// <c>EnableExternalControl</c> 會兩邊都判到「現在沒人控制」而互相蓋掉。
    /// 🔴 鎖內只做參照的讀寫:沒有 I/O、沒有 ImGui、沒有日誌、沒有對別的外掛的呼叫。
    /// 讀取端一律不進這把鎖(<c>Volatile.Read</c> 就夠),所以每幀的讀取不會與 IPC 端點爭鎖。
    /// </summary>
    private readonly object ExternalControlLock = new();

    /// <summary>
    /// 一次拿到整份外部控制狀態的原子快照。<b>只有真的處於外部控制時才回非 null</b>
    /// (條件與舊的 <c>IsInExternalControl()</c> 逐字相同),所以回傳值非 null 時
    /// <c>Requester</c> 與 <c>Config</c> 兩個都保證非 null,讀端不必再判一次。
    /// </summary>
    public ExternalControlState GetActiveExternalControl()
    {
        var state = System.Threading.Volatile.Read(ref this.CurrentExternalControl);
        return state != null && state.Requester != null && state.Config != null ? state : null;
    }

    /// <summary>等價於舊的 <c>Requester</c> 欄位(改為唯讀)。</summary>
    public string Requester => this.GetActiveExternalControl()?.Requester;

    /// <summary>等價於舊的 <c>ExternalConfig</c> 欄位(改為唯讀)。</summary>
    public ExternalTerritoryConfig ExternalConfig => this.GetActiveExternalControl()?.Config;

    /// <summary>
    /// 使用者在設定頁按「取消外部控制」時走這裡:一次清掉整份,
    /// 取代舊碼分兩行各指派一個欄位(那兩行中間就是讀端看得到的中間態)。
    /// </summary>
    public void ClearExternalControl()
    {
        lock (this.ExternalControlLock)
        {
            System.Threading.Volatile.Write(ref this.CurrentExternalControl, null);
        }
    }

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

    /// <remarks>
    /// 🔴 這支跑在<b>呼叫端的執行緒</b>上。判斷與寫入一起包在 <see cref="ExternalControlLock"/> 裡:
    /// 兩個外掛同時搶控制權時只有一個拿得到 <c>true</c>(舊碼兩個都會拿到 <c>true</c>,
    /// 真正生效的是後寫的那一個 —— 先搶到的外掛以為自己有控制權,失敗形式完全靜默)。
    /// <br/><br/>
    /// 這裡刻意<b>不</b>走 <see cref="RunOnFramework"/> 佇列:那條路不等待,回不了真正的答案,
    /// 對 <c>bool</c> 端點會把「我拿到控制權了嗎」變成瞎猜 —— 佇列只適用回傳 <c>void</c> 的端點。
    /// </remarks>
    [EzIPC]
    public bool EnableExternalControl(string requester, ExternalTerritoryConfig config)
    {
        lock (this.ExternalControlLock)
        {
            var current = System.Threading.Volatile.Read(ref this.CurrentExternalControl);
            var active = current != null && current.Requester != null && current.Config != null;
            if (!active || current.Requester == requester)
            {
                System.Threading.Volatile.Write(ref this.CurrentExternalControl, new ExternalControlState(requester, config));
                return true;
            }
            return false;
        }
    }

    /// <remarks>判斷條件與寫入的原子性同 <see cref="EnableExternalControl"/>。</remarks>
    [EzIPC]
    public bool DisableExternalControl(string requester)
    {
        lock (this.ExternalControlLock)
        {
            var current = System.Threading.Volatile.Read(ref this.CurrentExternalControl);
            var active = current != null && current.Requester != null && current.Config != null;
            if (!active || current.Requester == requester)
            {
                System.Threading.Volatile.Write(ref this.CurrentExternalControl, null);
                return true;
            }
            return false;
        }
    }

    /// <remarks>
    /// 單次 <c>Volatile.Read</c>,不進鎖 —— 這支被 framework 執行緒每幀問很多次,
    /// 讓它與 IPC 寫入端爭鎖是白費的。
    /// </remarks>
    [EzIPC]
    public bool IsInExternalControl() => this.GetActiveExternalControl() != null;

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

    /// <remarks>
    /// 🔴 這支跑在<b>呼叫端的執行緒</b>上,而 <c>P.BlockList</c> 是
    /// <c>Svc.PluginInterface.GetOrCreateData&lt;HashSet&lt;string&gt;&gt;()</c> 拿回來的
    /// <b>跨外掛共用</b>集合 —— 別的外掛隨時會在它們自己的執行緒上 Add／Remove／Clear。
    /// <br/><br/>
    /// 這裡刻意<b>不加鎖</b>:那個集合不是我們的,我們和其他持有者之間沒有共同的鎖協定,
    /// 自己加一把只鎖得住自己的鎖,只會給出「有保護」的假象。
    /// 只讀 <c>Count</c> 已經是最小暴露面(今天它是一個欄位讀取);外面包一層,
    /// 萬一走到集合內部不變式被打破而擲 <c>InvalidOperationException</c> 的路徑時,
    /// 回答「沒有被暫停」而不是把例外擲進呼叫端的碼裡。
    /// </remarks>
    [EzIPC]
    public bool IsPaused()
    {
        try
        {
            return P.BlockList.Count != 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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
    /// <summary>
    /// 「TextAdvance 的移動/互動佇列現在正在跑」的唯讀狀態。Questionable、AutoDuty 這類
    /// 消費端會<b>高頻輪詢</b>它。
    /// </summary>
    /// <remarks>
    /// 🔴 這支跑在<b>呼叫端的執行緒</b>上,而 <c>TaskManager.IsBusy</c> 問的是 ECommons
    /// <c>TaskManager</c> 的兩個裸 <c>List&lt;T&gt;</c>(ECommons 自己的註解就寫著
    /// only ever do that from Framework.Update event) —— framework 執行緒每幀在增刪它們。
    /// 從別的執行緒讀不只是「拿到舊值」,並行改動時看到的可能是集合內部不變式被打破的中間態。
    /// <br/><br/>
    /// 🔑 這裡刻意<b>不</b>走 <see cref="RunOnFramework"/> 的佇列:那條路不等待、回不了答案,
    /// 對 <c>bool</c> 端點沒有意義;也刻意不改成同步等 framework 執行緒 —— 高頻布林查詢
    /// 等一幀會把呼叫端的執行緒卡到下一次 Framework.Update。
    /// 改成讀 framework 執行緒每幀寫入的 <c>TextAdvance.IsBusySnapshot</c>,最舊差一幀,
    /// 作法與同檔 <see cref="IsEnabled"/> 一致。
    /// <br/><br/>
    /// 📌 已經在 framework 執行緒上時就地算,回傳值與時序逐字不變。
    /// 📌 卸載期不受影響:這條路徑一次都沒有呼叫 <c>RunOnFrameworkThread</c>,
    /// 所以沒有「<c>IsFrameworkUnloading</c> 為真時就地在呼叫端執行緒執行」那個旁路可踩。
    /// </remarks>
    [EzIPC]
    public bool IsBusy()
        => Svc.Framework.IsInFrameworkUpdateThread ? IsBusyCore() : P.IsBusySnapshot;

    /// <summary>
    /// <see cref="IsBusy"/> 的實際判斷,條件與改動前逐字相同。
    /// <b>只能在 framework 執行緒上呼叫。</b>
    /// </summary>
    internal static bool IsBusyCore() => S.EntityOverlay.TaskManager.IsBusy;
}
