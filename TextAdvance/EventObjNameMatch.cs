using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using static TextAdvance.SplatoonHandler;

namespace TextAdvance;

/// <summary>
/// <c>Markers.EventObjNameWhitelist.ContainsIgnoreCase(x.Name.ToString())</c> 的等價判斷，給每幀／導航迴圈用：
/// 按物件表格位記住上一次的原始名稱位元組（到第一個 NUL 為止）與結果，位元組逐字相同就重用。
/// </summary>
/// <remarks>
/// 結果只取決於名稱位元組（<c>IGameObject.Name</c>＝<c>SeString.Parse(Struct-&gt;Name)</c>），所以同格換物件、改名、
/// 同 BaseId 不同名都會落到「位元組不同」而照原式重算。只存位元組與結果，不存包裝或指標。
/// </remarks>
internal static unsafe class EventObjNameMatch
{
    private const int Stride = 64;
    private const int MaxSlots = 1024;
    private static readonly System.Threading.Lock Gate = new();
    private static readonly byte[] Names = new byte[MaxSlots * Stride];
    private static readonly short[] Lengths = CreateLengths();
    private static readonly bool[] Results = new bool[MaxSlots];

    private static short[] CreateLengths()
    {
        var ret = new short[MaxSlots];
        Array.Fill(ret, (short)-1);
        return ret;
    }

    public static bool IsWhitelisted(IGameObject x)
    {
        var o = x.Struct();
        int slot = o->ObjectIndex;
        Span<byte> snap = stackalloc byte[Stride];
        var len = ReadName(o, snap);
        if(slot >= MaxSlots || len < 0) return Compute(x);
        var name = snap[..len];
        lock(Gate)
        {
            if(Lengths[slot] == len && name.SequenceEqual(Names.AsSpan(slot * Stride, len))) return Results[slot];
        }
        var result = Compute(x);
        // 重算期間名稱若被改寫，這次的結果不一定對應快照，不存。
        Span<byte> again = stackalloc byte[Stride];
        var len2 = ReadName(o, again);
        if(len2 != len || !again[..len2].SequenceEqual(name)) return result;
        lock(Gate)
        {
            name.CopyTo(Names.AsSpan(slot * Stride, Stride));
            Lengths[slot] = (short)len;
            Results[slot] = result;
        }
        return result;
    }

    private static bool Compute(IGameObject x) => Markers.EventObjNameWhitelist.ContainsIgnoreCase(x.Name.ToString());

    /// <returns>名稱長度（不含 NUL）；欄位比快取格寬時回 -1（不走快取）。</returns>
    private static int ReadName(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* o, Span<byte> dst)
    {
        ReadOnlySpan<byte> raw = o->Name;
        var len = raw.IndexOf((byte)0);
        if(len < 0) len = raw.Length;
        if(len > dst.Length) return -1;
        raw[..len].CopyTo(dst);
        return len;
    }
}
