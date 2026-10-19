using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;

namespace TextAdvance.Helpers;

/// <summary>
/// 「這扇窗(位址)已經按過了」的共用守衛:同一扇窗在它走完生命週期之前只按一次。
/// 全外掛所有對 addon 的按法(<c>AddonMaster</c> 的 <c>Click()</c>/<c>Select()</c>/<c>HandOver()</c>、
/// <c>Callback.Fire</c>、<c>ClickAddonButton</c>、直送 <c>ReceiveEvent</c>)都要先問過 <see cref="TryPressOnce"/>。
/// </summary>
/// <remarks>
/// <para>
/// 🔴 這是在防一種 <c>try</c>/<c>catch</c> 攔不住的崩潰:addon 被按下之後有「正在關閉中」的幾幀,
/// <c>GetAddonByName</c> 仍然拿得到實例,<c>IsVisible</c> 與 <c>UldManager.LoadedState == Loaded</c>
/// 也都還成立(= <c>IsAddonReady</c> 三關全過),此時再送一次 callback/輸入事件就會踩到原生
/// AccessViolation(C0000005)。AVE 在 .NET Core 是 corrupted-state exception,<c>try</c>/<c>catch</c>
/// 完全無效 —— 唯一的防護是「不要送第二次」,不是「送了再接住」。
/// </para>
/// <para>
/// 🔴 節流<b>不是</b>這個防護:<c>EzThrottler</c> 記的是「上一次動作在哪個時刻」,不是「這扇窗已經按過」,
/// key 全域持久且<b>首次一定放行</b>;預設 500ms 在低 FPS 時可能短於一扇窗關閉所需的幀數。
/// 本外掛原有的 <c>EzThrottler.Throttle("Handin")</c> 之類全部保留,只是不再把它當防護。
/// </para>
/// <para>
/// 🔑 做法:按下之前先登記「這扇窗的哪一個實例位址(+哪一組參數)被按過」,在觀察到那扇窗
/// 真的走完生命週期之前不准再按。位址<b>只做等值比較,永遠不解參</b>——被記下的位址隨時可能已經失效。
/// </para>
/// <para>
/// 🔑 解除點是<b>雙軌</b>,兩條都只會讓封鎖<b>提早</b>解除:
/// <b>輪詢</b>(<see cref="Tick"/>):被記下的位址已經不在該名稱的 addon 清單裡 ⇒ 那扇窗真的收乾淨了(Framework.Update 驅動的按下點的主要解除點)。
/// <b>AddonLifecycle 事件</b>(<see cref="AddonEvent.PostSetup"/>,含位址重用;<see cref="AddonEvent.PreFinalize"/> 是<b>上鎖點</b>,鎖到位址消失或同位址重建):
/// 對 <c>ExecSkipTalk</c> 這種掛在 <c>PostSetup</c>+<c>PostUpdate</c> 上的按下點是<b>必要的</b> —— addon 不存在的那幾幀
/// <c>PostUpdate</c> 根本不會被叫到,而同名 addon 重開常常重用同一塊位址,只靠輪詢會把重開的那扇擋到逃生口。
/// ⚠️ 刻意<b>不</b>把 <c>PostRefresh</c> 當解除點:它可能在關閉中那幾幀觸發,會把防線提早拆掉。
/// </para>
/// <para>🔑 常駐視窗(<see cref="PersistentAddons"/>)位址永不消失、事件也不再來,改走第三條:按下後連續隱藏 N 幀。</para>
/// <para>
/// 🔑 粒度=(窗,位址,參數組):「回答一次即終結」的窗(SelectString 跳過過場確認、JournalResult 完成、
/// JournalAccept 接受、Request 交出、ContextIconMenu 選件)<b>不帶</b> <c>paramKey</c>,整扇窗一把 key。
/// 按下不會關的窗(Request 逐格填入 <c>(2, slot)</c>)帶 <c>paramKey</c>,同一扇窗對不同參數組可以各按一次;
/// 但只要這扇窗<b>不帶參數的</b> key 已經記下這個位址(=我們自己按了交出),任何參數組都不准再送。
/// </para>
/// <para>
/// 🔑 逃生口防死鎖:永久封鎖會讓 <c>LegacyTaskManager</c>(<c>AbortOnTimeout=true</c>)的任務卡到逾時、
/// 清掉整條佇列。單答終結窗取 <see cref="RePressEscapeFrames"/>(60 幀,遠大於關閉所需),走到代表
/// 「按了卻沒關掉」,寫 <c>Information</c>(要使用者回報的診斷);Talk 類多次互動窗取
/// <see cref="RoutineRePressEscapeFrames"/>(15 幀),走逃生口是翻頁的常態,寫 <c>Debug</c> 不洗版。
/// </para>
/// <para>
/// 📌 回 <see langword="false"/> 對呼叫端的意義一律是「這一幀沒按到,下一幀再來」,與「addon 還沒出現」
/// 「節流還沒放行」走同一條既有路徑,不改變任何呼叫端的控制流;🔴 對 <c>bool?</c> 任務絕不回 <see langword="null"/>
/// (那是 Abort,會清掉整條佇列)。正常路徑零變化:第一次看到某扇窗一律當場按下去。
/// </para>
/// <para>⚠️ 只在主執行緒使用(與呼叫端的 <c>EzThrottler</c> 同一個前提)。</para>
/// </remarks>
internal static class AddonPressGuard
{
    /// <summary>已經按過、那扇窗卻還沒消失時,最多再等這麼多幀才允許補按一次(單答終結窗)。</summary>
    /// <remarks>
    /// 🔑 這不是節流 —— 真正的防護是「同一扇窗只按一次」,這個值只是防死鎖的逃生口。
    /// 60 幀(60fps 下約 1 秒)遠遠大於「關閉中的那幾幀」,補按永遠不會落在危險窗口內。
    /// 用幀數而不是毫秒:危險窗口本來就是以幀計的,遊戲卡頓時兩者一起拉長。
    /// 📌 數的是 <see cref="CurrentFrame"/>(守衛自己掛在 <c>Framework::Tick</c> 上的時鐘),
    /// 不是 <c>UiBuilder.FrameCount</c>(那個在過場/隱藏 UI 時會凍結,理由見 <see cref="frameCount"/>)。
    /// </remarks>
    internal const int RePressEscapeFrames = 60;

    /// <summary>
    /// 「按一次翻一頁、窗不會因為被按而消失」的多次互動窗(Talk 是代表)專用逃生口:
    /// <see cref="TryPressOnce"/> 的 <c>escapeIsRoutine</c> 為 <see langword="true"/> 時用它取代 <see cref="RePressEscapeFrames"/>。
    /// </summary>
    /// <remarks>
    /// 🔑 這類窗走逃生口是常態(那才是翻到下一頁的方式),逃生口長度直接決定節奏。
    /// 關閉中的危險窗口實測 &lt;10 幀,15 幀不落在裡面;每頁 +0.25s 幾乎無感(使用者裁決過的取捨)。
    /// 📌 同樣數 <see cref="CurrentFrame"/>:過場動畫裡的對話正是靠這個時鐘才翻得動頁。
    /// ⚠️ 判準刻意<b>不</b>用「文字變了」當翻頁證據:關閉中文字會讀壞(U+FFFD)。
    /// (文字只拿來判「這扇窗到底有沒有在變」、要不要退避;按不按仍然只看幀數。)
    /// </remarks>
    internal const int RoutineRePressEscapeFrames = 15;

    /// <summary>同一個(key,位址)連續補按這麼多次之後改用指數退避;在那之前的節奏完全不變。</summary>
    /// <remarks>
    /// 補的是「逃生口本身沒有上限」這個洞:窗還在、按了卻什麼都沒發生時會一直補按下去。
    /// 真的翻得動的窗每次補按都會換內容,計數被內容指紋歸零,永遠走不到退避。
    /// </remarks>
    internal const int RePressBackoffAfter = 8;

    /// <summary>退避後的逃生口上限(幀);封頂之後仍然會補按,只是頻率極低。</summary>
    internal const int MaxRePressEscapeFrames = 600;

    /// <summary>輪詢解除時最多掃到第幾個同名實例(遊戲同名實例上限);掃到第一個空的就提早停,掃滿仍沒停＝不知道、不解除。</summary>
    private const int MaxAddonIndex = 256;

    /// <summary>
    /// 常駐視窗:關閉只是隱藏,位址整場留在清單裡,PreFinalize/PostSetup 都不會再發生,輪詢與事件兩條解除點對它永遠不成立。
    /// 這些窗改由「按下後連續隱藏 <see cref="HiddenReleaseFrames"/> 幀」解除,而且送出前必須看得見(兩者互為反面)。
    /// </summary>
    /// <remarks>
    /// ⚠️ 只收實機證實常駐的名字:把會被銷毀的窗誤收進來,等於在它拆除途中提早解除封鎖。
    /// 判斷與 TCToolbox 的 <c>AddonPressGuard.PersistentAddons</c> 相同。
    /// </remarks>
    private static readonly HashSet<string> PersistentAddons = new(StringComparer.Ordinal) { "ContextIconMenu", "ContextMenu", "Talk" };

    /// <summary>常駐視窗按下後,要連續觀察到「還在清單裡、但已隱藏」這麼多幀才解除;看到可見就歸零重數。</summary>
    private const int HiddenReleaseFrames = 20;

    /// <summary>一個位址的按壓記錄。</summary>
    /// <remarks>
    /// <c>Frame</c>=最後一次送出按壓的幀;<c>RePresses</c>=這個位址走過幾次逃生口補按;
    /// <c>ContentToken</c>=呼叫端給的內容指紋,<c>0</c>=呼叫端沒給,那就永遠不會判定成「內容變了」。
    /// <c>HiddenFrames</c>=常駐視窗自最後一次按下起連續隱藏的幀數;每次按下都重建記錄,所以一定從 0 數起。
    /// </remarks>
    private sealed class PressRecord
    {
        public long Frame;
        public int RePresses;
        public int ContentToken;
        public int HiddenFrames;
    }

    /// <summary>
    /// 一把 key(窗名+參數組)底下「已經按過的位址 → 按下當時的幀」。同名窗可能同時開好幾扇,所以是集合不是單一格。
    /// </summary>
    private sealed class Slot
    {
        public string AddonName;
        public readonly Dictionary<nint, PressRecord> Pressed = new();
    }

    private static readonly Dictionary<string, Slot> Slots = new(StringComparer.Ordinal);

    /// <summary>看過 <see cref="AddonEvent.PreFinalize"/> 的(窗名,位址):位址消失或同位址重建之前任何按法都不准,沒有逃生口。</summary>
    /// <remarks>受守護的窗名一進 PreFinalize 就上鎖,不管我們按過沒有。</remarks>
    private static readonly HashSet<(string Name, nint Address)> Finalizing = [];

    /// <summary>窗名 → 已掛上的 PreFinalize/PostSetup 解除監聽器(掛上就不拆,只在 <see cref="ForceTeardown"/> 拆)。</summary>
    private static readonly Dictionary<string, IAddonLifecycle.AddonEventDelegate> Watchers = new(StringComparer.Ordinal);

    // 可重用緩衝:沒有窗被記著時 Tick 是一個整數比較就回來,不配置任何東西。
    private static readonly List<string> NamesBuf = [];
    private static readonly HashSet<nint> PresentBuf = [];
    private static readonly List<nint> RemoveBuf = [];
    private static readonly List<string> EmptyKeysBuf = [];
    private static readonly List<string> LifecycleEmptyKeysBuf = [];
    private static readonly HashSet<nint> HiddenBuf = [];
    private static readonly List<(string Name, nint Address)> UnlockBuf = [];

    /// <summary>「這個窗名第一次走隱藏解除」只寫一行 Information。</summary>
    private static readonly HashSet<string> HiddenReleaseReported = new(StringComparer.Ordinal);

    /// <summary>守衛自己的幀時鐘:由 <c>Svc.Framework.Update</c>(原生 <c>Framework::Tick</c>)每個遊戲幀遞增。</summary>
    /// <remarks>
    /// 🔴🔴 這裡刻意<b>不</b>用 <c>Svc.PluginInterface.UiBuilder.FrameCount</c> —— 那個計數器
    /// <b>在外掛 UI 被隱藏時完全停止前進</b>:本 pin 的 <c>UiBuilder.OnDraw</c> 判定「使用者隱藏 UI /
    /// <b>過場動畫中</b> / GPose」三者任一成立時<b>直接 return</b>,而 <c>FrameCount++</c> 寫在那個
    /// return <b>之後</b>;其中過場那條的開關 <c>ToggleUiHideDuringCutscenes</c> <b>預設是開的</b>,
    /// 而本外掛沒有設定任何 <c>DisableCutsceneUiHide</c>/<c>DisableAutomaticUiHide</c> 去豁免。
    /// <para>
    /// ⇒ 用它當時鐘的話,過場中時鐘凍結在某一格 F,<c>frame - pressedAt</c> 恆等於 0、逃生口<b>永遠等不完</b>,
    /// 而按下點照樣每幀被叫到(<c>ExecSkipTalk</c> 掛在 <c>AddonLifecycle PostUpdate</c>,那是原生
    /// <c>AtkUnitBase::Update</c> 的 detour,與 <c>OnDraw</c> 完全無關)⇒ 過場中每場對話只會被自動推進
    /// <b>第一頁</b>,之後永久卡住,而且三條解除點全數無效(輪詢看到窗還在不解除;同一場對話翻頁不觸發
    /// PostSetup;<see cref="OnLifecycle"/> 的同幀豁免 <c>pressedAt >= frame</c> 在凍結時恆成立)。
    /// </para>
    /// <para>
    /// 📌 兩個時鐘在<b>沒被隱藏</b>的正常狀況下都是「每個遊戲幀 +1」(遊戲每 tick 出一張畫面),
    /// 所以 <see cref="RePressEscapeFrames"/>/<see cref="RoutineRePressEscapeFrames"/> 的幀數語意不變,
    /// <b>不需要跟著調整</b>;差別只在「被隱藏時 <c>UiBuilder.FrameCount</c> 會停、這個不會」。
    /// </para>
    /// <para>
    /// ⚙️ 遞增委派是在 <c>TextAdvance.Tick</c> 的委派<b>之後</b>才加進 <c>Framework.Update</c> 派送清單的,而遊戲的 tick 本體
    /// (<c>AtkUnitBase::Update</c> 也在裡面)在 Update 事件派送完之後才跑 ⇒ 同一個遊戲幀裡 <b>Framework 路徑</b>(各 <c>Exec*.Tick</c>)
    /// 讀到的值比 <b>AddonLifecycle 路徑</b>(<c>ExecSkipTalk.Click</c>、<see cref="OnLifecycle"/>)<b>少 1</b>;差值固定,而所有比較都在同一條路徑內部,所以無害。
    /// <see cref="OnLifecycle"/> 的同幀豁免只服務 <c>ExecSkipTalk</c>(按下與豁免都在 AddonLifecycle 路徑、讀到同一個值);
    /// Framework 路徑的按下在同一個遊戲幀讀到的值較小,位址重用時的 PostSetup 一定會清掉它,不受豁免影響。
    /// </para>
    /// </remarks>
    private static long frameCount;

    /// <summary>幀時鐘是否已經掛上(<see cref="EnsureClock"/> 冪等)。</summary>
    private static bool clockRunning;

    /// <summary>守衛的幀時鐘;<b>單調不減</b>,只做差值與大小比較。</summary>
    internal static long CurrentFrame => frameCount;

    /// <summary>掛上幀時鐘(冪等)。</summary>
    /// <remarks>
    /// 🔴 從<b>兩個</b>互相獨立的地方叫:<see cref="Tick"/> 的第一行(每幀無條件、外掛一載入就在跑)
    /// 與 <see cref="EnsureWatching"/>(任何按下點的必經之路)。兩條同時斷掉時鐘才會停,
    /// 而那時也已經沒有人在按東西了。
    /// ⚠️ 特別是 <see cref="CurrentFrame"/> 有<b>不經過 <see cref="TryPressOnce"/> 的讀者</b>
    /// (<c>ExecRequestComplete</c> 開窗後的等待):時鐘若只在按下路徑上懶啟動,那個讀者會永遠讀到 0、
    /// 卡死在自己的等待條件裡,連帶永遠不會走到按下路徑去啟動時鐘。
    /// </remarks>
    private static void EnsureClock()
    {
        if(clockRunning) return;
        clockRunning = true;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private static void OnFrameworkUpdate(IFramework framework) => frameCount++;

    /// <summary>
    /// 從窗上讀出來的文字含 U+FFFD(替換字元)= 這幾幀窗的記憶體正在變動(多半是關閉中),
    /// 凡是靠文字做判定的按下點<b>這一幀不要碰</b>。這是實機崩潰前 log 裡看到的旁證。
    /// </summary>
    /// <returns><see langword="true"/> = 文字讀壞了,呼叫端這一幀什麼都不要做。</returns>
    internal static bool TextIsUnstable(string addonName, string text)
    {
        if(text == null || text.IndexOf('\uFFFD') < 0) return false;
        if(EzThrottler.Throttle($"AddonPressGuard-Corrupt-{addonName}", 1000))
        {
            PluginLog.Information($"[AddonPressGuard] 「{addonName}」的文字讀到 U+FFFD 亂碼(視窗記憶體正在變動,多半是關閉中),這一幀不碰它。");
        }
        return true;
    }

    /// <summary>
    /// 問「這扇窗現在可以按嗎」,可以的話<b>順便記下</b>已經按過。呼叫端拿到 <see langword="true"/> 才去按,
    /// 按法留給呼叫端。呼叫點要放在<b>緊接著送出動作之前</b>(節流之後):一回 <see langword="true"/> 就已經記下,
    /// 登記完卻不按會白白封鎖到逃生口。
    /// </summary>
    /// <param name="addonName">窗名。是輪詢與生命週期監聽器解除封鎖用的名字,也是 key 的前半。</param>
    /// <param name="addon">要按的 addon 位址。<b>只做等值比較,這裡永遠不解參。</b></param>
    /// <param name="label">寫進 log 的名字;省略就用 key。</param>
    /// <param name="paramKey">
    /// <see langword="null"/>(預設)=「回答一次即終結」的窗,整扇窗一把 key;
    /// 非空 = 按下不會關的窗,同一扇窗對不同參數組各准按一次。
    /// </param>
    /// <param name="escapeIsRoutine">
    /// <see langword="true"/> = 這個按下點「同一扇窗本來就會被按很多次」(Talk 翻頁、Request 逐格填入的重試),
    /// 逃生口縮成 <see cref="RoutineRePressEscapeFrames"/>,走逃生口是常態寫 <c>Debug</c>;
    /// <see langword="false"/>(預設)= 走逃生口代表「按了卻沒關掉」這種該被回報的異常,寫 <c>Information</c>。
    /// </param>
    /// <param name="contentToken">
    /// 這扇窗目前顯示內容的指紋;跟上一次不同 = 上一次按壓真的生效了,補按計數歸零。
    /// <c>0</c>(預設)= 呼叫端沒有指紋可給,計數只會在位址消失時隨記錄一起消掉。
    /// </param>
    /// <returns><see langword="true"/> = 可以按(而且已經記下);<see langword="false"/> = 這一幀不要按。</returns>
    internal static bool TryPressOnce(string addonName, nint addon, string label = null, string paramKey = null, bool escapeIsRoutine = false, int contentToken = 0)
    {
        if(addon == 0 || string.IsNullOrEmpty(addonName)) return false;
        EnsureWatching(addonName);
        // 常駐窗的解除條件是「連續隱藏」,這裡是它的反面:隱藏中(或這一幀查不到)的常駐窗一律不送。
        if(PersistentAddons.Contains(addonName) && !IsListedAndVisible(addonName, addon)) return false;
        var frame = CurrentFrame;
        var key = paramKey == null ? addonName : addonName + "|" + paramKey;
        var tag = label ?? key;
        if(Finalizing.Contains((addonName, addon)))
        {
            LogHold(addonName, addon, tag + "(窗正在銷毀)", routine: false);
            return false;
        }
        if(paramKey != null && Slots.TryGetValue(addonName, out var answered) && answered.Pressed.TryGetValue(addon, out var answeredRec))
        {
            // 這扇窗已經被「回答」過(我們自己按了交出/確認)。窗還在 = 正在關閉中,任何參數組都不准再送。
            // 超過逃生口仍在的話交給不帶參數那把 key 自己去判,這裡放行。
            if(frame - answeredRec.Frame < RePressEscapeFrames)
            {
                LogHold(addonName, addon, tag + "(窗已回答)", routine: false);
                return false;
            }
        }
        if(!Slots.TryGetValue(key, out var slot))
        {
            slot = new() { AddonName = addonName };
            Slots[key] = slot;
        }
        var rePresses = 0;
        if(slot.Pressed.TryGetValue(addon, out var rec))
        {
            // 內容換了 = 上一次按壓真的翻到下一頁,補按計數歸零,長對話不會被退避拖慢。
            rePresses = contentToken != 0 && contentToken != rec.ContentToken ? 0 : rec.RePresses;
            // 這一扇已經按過。窗還在 = 可能正在關閉中,此時再按就是上面說的 AVE。
            if(frame - rec.Frame < EscapeFramesFor(escapeIsRoutine, rePresses))
            {
                LogHold(addonName, addon, tag, escapeIsRoutine);
                return false;
            }
            // 逃生口:等了遠超過關閉所需的時間,窗仍在。視為那次沒生效(或這是另一扇重用了同一塊位址、
            // 而兩條解除點都沒看到的新窗),放行補按一次。
            rePresses++;
            if(rePresses == 1 || rePresses % 50 == 0)
            {
                var msg = $"[AddonPressGuard] {tag}(實例 0x{addon:X}):按下後 {frame - rec.Frame} 幀仍是同一扇窗,補按一次。累計 {rePresses} 次。";
                if(escapeIsRoutine) PluginLog.Debug(msg); else PluginLog.Information(msg);
            }
            if(rePresses == RePressBackoffAfter)
            {
                PluginLog.Information($"[AddonPressGuard] {tag}(實例 0x{addon:X}):補按 {rePresses} 次都沒讓這扇窗有任何變化,改用指數退避,逃生口最長拉到 {MaxRePressEscapeFrames} 幀。");
            }
        }
        slot.Pressed[addon] = new() { Frame = frame, RePresses = rePresses, ContentToken = contentToken };
        // 跨外掛重按診斷：只在真的送出按壓時記一行，刻意不節流。
        PluginLog.Verbose($"[按窗診斷] plugin=TextAdvance addon={addonName} addr=0x{addon:X} key={paramKey}");
        return true;
    }

    /// <summary>
    /// 提前掛上某個窗名的解除監聽器。<see cref="TryPressOnce"/> 本來就會在第一次用到時掛,
    /// 這支是給「按下點自己也掛在同一個 addon 的 <c>PostSetup</c> 上」的模組用的(<c>ExecSkipTalk</c>):
    /// ⚠️ 本 pin 的 <c>RegisterListener</c> 走 <c>RunOnTick</c>、不保序,先註冊不保證先被叫到 ——
    /// 只是讓守衛的 PostSetup 解除盡量排在按下點之前;真正兜底的是 <see cref="OnLifecycle"/> 的同幀豁免。
    /// </summary>
    internal static void Watch(string addonName) => EnsureWatching(addonName);

    /// <summary>
    /// 每幀從 <c>TextAdvance.Tick</c> 最前面無條件呼叫:被記下的位址已經從該窗名的清單裡消失時解除封鎖。
    /// </summary>
    /// <remarks>
    /// 🔴 存下來的位址只做等值比較,<b>永遠不解參</b>(常駐窗的可見性讀的是這一幀 <c>GetAddonByName</c> 交回來的指標)。
    /// 判準刻意<b>不</b>用「文字還對不對」或「還可不可見」:窗在拆除途中可能有幾幀讀不到文字、或已經被設成不可見,
    /// 拿那些當「窗不見了」會<b>正好在最危險的那幾幀</b>把封鎖解除掉。唯一例外是 <see cref="PersistentAddons"/>。
    /// 放在 Tick 最前面且不受任何開關限制:解除點若只長在各自的分支裡,開關剛好在按下之後轉為關閉時
    /// 記號會一直留著,下一扇重用同一塊位址的窗會被白白擋到逃生口。
    /// </remarks>
    internal static void Tick()
    {
        // 🔴 時鐘要掛在「沒有記號就回頭」那一行之前:掛在後面的話,沒有窗被記著時時鐘就停住,
        //    不經過按下路徑的讀者(ExecRequestComplete 開窗後的等待)會卡死。
        EnsureClock();
        if(Slots.Count == 0 && Finalizing.Count == 0) return;
        NamesBuf.Clear();
        EmptyKeysBuf.Clear();
        foreach(var (key, slot) in Slots)
        {
            if(slot.Pressed.Count == 0)
            {
                EmptyKeysBuf.Add(key);
                continue;
            }
            if(!NamesBuf.Contains(slot.AddonName)) NamesBuf.Add(slot.AddonName);
        }
        foreach(var (lockedName, _) in Finalizing)
        {
            if(!NamesBuf.Contains(lockedName)) NamesBuf.Add(lockedName);
        }
        foreach(var name in NamesBuf)
        {
            var persistent = PersistentAddons.Contains(name);
            PresentBuf.Clear();
            HiddenBuf.Clear();
            var scanComplete = false;
            for(var i = 1; i <= MaxAddonIndex; i++)
            {
                var unit = Svc.GameGui.GetAddonByName(name, i);
                if(unit.IsNull)
                {
                    scanComplete = true;
                    break;
                }
                PresentBuf.Add(unit.Address);
                if(persistent && !unit.IsVisible) HiddenBuf.Add(unit.Address);
            }
            // 掃滿上限仍沒看到盡頭＝清單不完整,這一幀這個窗名什麼都不解除。
            if(!scanComplete) continue;
            UnlockBuf.Clear();
            foreach(var locked in Finalizing)
            {
                if(locked.Name == name && !PresentBuf.Contains(locked.Address)) UnlockBuf.Add(locked);
            }
            foreach(var locked in UnlockBuf) Finalizing.Remove(locked);
            foreach(var (key, slot) in Slots)
            {
                if(slot.AddonName != name || slot.Pressed.Count == 0) continue;
                RemoveBuf.Clear();
                foreach(var (addr, rec) in slot.Pressed)
                {
                    if(!PresentBuf.Contains(addr))
                    {
                        RemoveBuf.Add(addr);
                        continue;
                    }
                    if(!persistent) continue;
                    // 只有「在清單裡而且隱藏」才累加,看得見就歸零:關閉中那幾幀窗還看得見,永遠累不滿。
                    var hidden = HiddenBuf.Contains(addr) ? rec.HiddenFrames + 1 : 0;
                    if(hidden >= HiddenReleaseFrames)
                    {
                        RemoveBuf.Add(addr);
                        ReportHiddenRelease(name);
                    }
                    else
                    {
                        rec.HiddenFrames = hidden;
                    }
                }
                foreach(var addr in RemoveBuf) slot.Pressed.Remove(addr);
                if(slot.Pressed.Count == 0) EmptyKeysBuf.Add(key);
            }
        }
        // 空掉的 key 順手收掉,帶動態參數組的 key(fill:{slot})才不會累積。
        foreach(var key in EmptyKeysBuf) Slots.Remove(key);
    }

    /// <summary>外掛卸載時硬拆所有監聽器(不留指向本組件的委派)並清掉所有記號。</summary>
    internal static void ForceTeardown()
    {
        if(clockRunning)
        {
            Svc.Framework.Update -= OnFrameworkUpdate;
            clockRunning = false;
        }
        // frameCount 刻意不歸零:歸零會讓殘留記號的 pressedAt 大於當下幀而算出負的差值。
        foreach(var (addonName, handler) in Watchers)
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, addonName, handler);
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, addonName, handler);
        }
        Watchers.Clear();
        Slots.Clear();
        Finalizing.Clear();
    }

    /// <summary>被擋那一幀的診斷:單答窗寫 Information(要使用者回報的診斷)、每扇窗 1 秒節流;多次互動窗的等待是常態,不寫。</summary>
    private static void LogHold(string addonName, nint addon, string tag, bool routine)
    {
        if(routine) return;
        if(EzThrottler.Throttle($"AddonPressGuard-Hold-{addonName}", 1000))
        {
            PluginLog.Information($"[AddonPressGuard] {tag}(實例 0x{addon:X}):按過之後還沒觀察到它收掉,這一幀不再碰它 —— 對關閉中的視窗送 callback 是攔不到的存取違規。");
        }
    }

    /// <summary>在同名清單裡找到 <paramref name="addon"/>,而且它這一幀看得見。</summary>
    /// <remarks>讀的是 <c>GetAddonByName</c> 這一幀交回來的指標;<paramref name="addon"/> 本身只做等值比較。查不到一律回 <see langword="false"/>。</remarks>
    private static bool IsListedAndVisible(string addonName, nint addon)
    {
        for(var i = 1; i <= MaxAddonIndex; i++)
        {
            var unit = Svc.GameGui.GetAddonByName(addonName, i);
            if(unit.IsNull) return false;
            if(unit.Address == addon) return unit.IsVisible;
        }
        return false;
    }

    /// <summary>隱藏解除第一次接上時記一行(每個窗名每次遊戲一行),實機 log 靠它確認這條路真的走得到。</summary>
    private static void ReportHiddenRelease(string addonName)
    {
        if(!HiddenReleaseReported.Add(addonName)) return;
        PluginLog.Information($"[AddonPressGuard] 「{addonName}」是常駐視窗（隱藏而不銷毀），按下記號改由「連續隱藏 {HiddenReleaseFrames} 幀」解除。這一行每個視窗名每次遊戲只寫一次。");
    }

    /// <summary>第一次守護某個窗名時掛上解除封鎖用的監聽器;掛上之後就不再拆(只在 <see cref="ForceTeardown"/> 拆)。</summary>
    private static void EnsureWatching(string addonName)
    {
        EnsureClock();
        if(Watchers.ContainsKey(addonName)) return;
        IAddonLifecycle.AddonEventDelegate handler = (type, args) => OnLifecycle(addonName, type, args);
        Watchers[addonName] = handler;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, addonName, handler);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, handler);
    }

    /// <summary>該位址進入銷毀(PreFinalize:上鎖)或重新開始(PostSetup:解鎖,並清掉這個窗名底下所有 key 對它的記號)。</summary>
    /// <remarks>
    /// 🔴 只讀 <c>args.Addon.Address</c> 做等值比較,不解參。
    /// ⚠️ PostSetup 時<b>同一幀</b>才登記的記號要留著:那是按下點自己也掛在 PostSetup 上、而且排在守衛之前被叫到時
    /// 對「這扇新窗」的按下(監聽器之間的呼叫順序沒有保證,見 <see cref="Watch"/>)。清掉它會讓下一幀 PostUpdate 再按一次 ——
    /// 單頁 Talk 的第一次按下就把窗關了,那第二次正好落在關閉中。
    /// </remarks>
    private static void OnLifecycle(string addonName, AddonEvent type, AddonArgs args)
    {
        var address = args.Addon.Address;
        if(address == 0) return;
        if(type == AddonEvent.PreFinalize)
        {
            LockFinalizing(addonName, address);
            return;
        }
        Finalizing.Remove((addonName, address));
        if(Slots.Count == 0) return;
        var frame = CurrentFrame;
        LifecycleEmptyKeysBuf.Clear();
        foreach(var (key, slot) in Slots)
        {
            if(slot.AddonName != addonName) continue;
            if(!slot.Pressed.TryGetValue(address, out var rec)) continue;
            if(rec.Frame >= frame) continue;
            slot.Pressed.Remove(address);
            if(slot.Pressed.Count == 0) LifecycleEmptyKeysBuf.Add(key);
        }
        foreach(var key in LifecycleEmptyKeysBuf) Slots.Remove(key);
    }

    /// <summary>鎖住正在銷毀的位址;只加不刪,解鎖交給 <see cref="Tick"/> 的消失輪詢與同位址 PostSetup。</summary>
    private static void LockFinalizing(string addonName, nint address) => Finalizing.Add((addonName, address));

    /// <summary>逃生口長度:前 <see cref="RePressBackoffAfter"/> 次補按維持原值,之後每次加倍,到 <see cref="MaxRePressEscapeFrames"/> 封頂。</summary>
    private static int EscapeFramesFor(bool routine, int rePresses)
    {
        var frames = routine ? RoutineRePressEscapeFrames : RePressEscapeFrames;
        if(rePresses < RePressBackoffAfter) return frames;
        var scaled = (long)frames << Math.Min(rePresses - RePressBackoffAfter + 1, 20);
        return scaled >= MaxRePressEscapeFrames ? MaxRePressEscapeFrames : (int)scaled;
    }

    /// <summary>純觀測:呼叫端讀到這扇窗一個字都沒有時記一行(每個窗名 5 秒一則);<b>不改變任何行為</b>,補按照常。</summary>
    internal static void LogAddonHasNoText(string addonName, nint addon, string tag)
    {
        if(EzThrottler.Throttle($"AddonPressGuard-NoText-{addonName}", 5000))
        {
            PluginLog.Information($"[AddonPressGuard] {tag}(實例 0x{addon:X}):這扇窗目前一個字都沒有(補按照常)。");
        }
    }
}
