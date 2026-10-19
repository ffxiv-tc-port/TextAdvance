using ECommons.EzHookManager;

namespace TextAdvance;

public unsafe class Memory
{
    private delegate nint AtkComponentJournalCanvas_ReceiveEventDelegate(nint a1, ushort a2, int a3, JournalCanvasInputData* a4, void* a5);
    [EzHook("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 4C 24 ?? 55")]
    private EzHook<AtkComponentJournalCanvas_ReceiveEventDelegate> AtkComponentJournalCanvas_ReceiveEventHook;

    internal delegate nint IsFlightProhibitedDelegate(nint a1);
    // 沒解到＝null。唯一的讀取點是 Utils.CanFly，它先判 CanCheckFlight。
    internal IsFlightProhibitedDelegate IsFlightProhibited;
    internal nint FlightAddr = Svc.SigScanner.TryScanText("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75 11", out var result) ? result : default;

    /// <summary>false ＝ 判斷不了能不能飛；<see cref="Utils.CanFly"/> 此時一律回「不能飛」。</summary>
    internal bool CanCheckFlight => this.IsFlightProhibited != null && this.FlightAddr != 0;

    /// <summary>false 時 <see cref="PickRewardItemUnsafe"/> 什麼都不做（它靠這個 hook 的 Original 把選取交給遊戲）。</summary>
    internal bool CanPickReward => this.AtkComponentJournalCanvas_ReceiveEventHook != null;

    private Memory()
    {
        EzSignatureHelper.Initialize(this);
        if (Svc.SigScanner.TryScanText("40 53 48 83 EC 20 48 8B 1D ?? ?? ?? ?? 48 85 DB 0F 84 ?? ?? ?? ?? 80 3D", out var isFlightProhibited)) this.IsFlightProhibited = EzDelegate.Get<IsFlightProhibitedDelegate>(isFlightProhibited);
        // 只在載入時印一次：每行＝一項沒對上的特徵碼，與因此停掉的功能。
        if (!this.CanCheckFlight) PluginLog.Warning($"[TextAdvance] 特徵碼對不上（{nameof(this.IsFlightProhibited)}／{nameof(this.FlightAddr)}）：判斷不了能不能飛，一律當成不能飛：任務導航不起飛、改走地面。");
        if (!this.CanPickReward) PluginLog.Warning($"[TextAdvance] 特徵碼對不上（{nameof(this.AtkComponentJournalCanvas_ReceiveEventHook)}）：自動挑選任務獎勵停用，任務完成視窗的自選獎勵要自己點。");
    }

    internal void PickRewardItemUnsafe(nint canvas, int index)
    {
        if (canvas < 1024)
        {
            DuoLog.Error($"TextAdvance anti-crash: canvas={canvas:X16} invalid data detected. Please report to developer.");
            return;
        }
        if (!this.CanPickReward) return;
        var emptyBytes = stackalloc byte[50];
        var data = stackalloc JournalCanvasInputData[1];
        this.AtkComponentJournalCanvas_ReceiveEventDetour(canvas, 9, 7 + index, data, emptyBytes);
    }

    private nint AtkComponentJournalCanvas_ReceiveEventDetour(nint a1, ushort a2, int a3, JournalCanvasInputData* a4, void* a5)
    {
        PluginLog.Information($"AtkComponentJournalCanvas_ReceiveEventDetour: Canvas ptr: {a1:X16}");
        var ret = this.AtkComponentJournalCanvas_ReceiveEventHook.Original(a1, a2, a3, a4, a5);
        try
        {
            var d = (JournalCanvasInputData*)a4;
            PluginLog.Debug($"AtkComponentJournalCanvas_ReceiveEventDetour: {(nint)a1:X16}, {a2}, {a3}, {(nint)a4:X16} ({d->Unk0}, {d->Unk4}, {d->Unk6}), {(nint)a5:X16}");
        }
        catch (Exception e)
        {
            e.Log();
        }
        return ret;
    }
}
