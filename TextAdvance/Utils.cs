using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.SplatoonAPI;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using static TextAdvance.SplatoonHandler;

namespace TextAdvance;
public static unsafe class Utils
{
    private static bool SplatoonAvailableInternal = false;
    public static bool SplatoonAvailable
    {
        get
        {
            if(EzThrottler.Throttle("SplatoonCheck", 10000))
            {
                SplatoonAvailableInternal = Svc.PluginInterface.InstalledPlugins.Any(x => x.InternalName == "Splatoon" && x.IsLoaded && Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.IsLoaded").HasFunction);
            }
            return SplatoonAvailableInternal;
        }
    }

    public static uint GetInstallationID()
    {
        var uniqueId = Svc.PluginInterface.AssemblyLocation.FullName;
        uniqueId += Environment.ProcessPath ?? "";
        uniqueId += Environment.UserName;
        var hashedId = Lumina.Misc.Crc32.Get(uniqueId);
        return hashedId;
    }

    public static bool ShouldHideUI()
    {
        return Svc.Condition[ConditionFlag.Occupied]
       || Svc.Condition[ConditionFlag.Occupied30]
       || Svc.Condition[ConditionFlag.Occupied33]
       || Svc.Condition[ConditionFlag.Occupied38]
       || Svc.Condition[ConditionFlag.Occupied39]
       || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
       || Svc.Condition[ConditionFlag.OccupiedInEvent]
       || Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
       || Svc.Condition[ConditionFlag.OccupiedSummoningBell]
       || Svc.Condition[ConditionFlag.WatchingCutscene]
       || Svc.Condition[ConditionFlag.WatchingCutscene78]
       || Svc.Condition[ConditionFlag.BetweenAreas]
       || Svc.Condition[ConditionFlag.BetweenAreas51];
    }

    public static void GetEligibleMapMarkerLocationsAsync(Action<List<Vector3>> callback)
    {
        // AgentHUD.Instance() 走 CS 的 [Agent] 產生器(agentModule == null ? null : ...),
        // UIModule 尚未建立或 HUD 代理人尚未配置時合法會回 null;解參考它是攔不到的 AccessViolation。
        // 取不到就不啟動這次非精確導航(本方法原本就有「耗時過久則丟棄結果、不回呼」的路徑,
        // 呼叫端不依賴 callback 必被呼叫)。
        var hud = AgentHUD.Instance();
        if (hud == null)
        {
            PluginLog.Warning("[TextAdvance] AgentHUD unavailable; skipping map marker scan");
            return;
        }
        var markers = hud->MapMarkers.AsSpan().ToArray();
        var playerPos = Player.Object.Position;
        Task.Run(() =>
        {
            var time = Environment.TickCount64;
            var ret = new List<Vector3>();
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var id = marker.IconId;
                if (SplatoonHandler.Markers.Map.MSQ.Contains(id) || SplatoonHandler.Markers.Map.ImportantSideProgress.Contains(id))
                {
                    ret.Add(marker.Position);
                }
                else if (SplatoonHandler.Markers.Map.MSQXZ.Contains(id))
                {
                    PluginLog.Debug($"Marker {marker.Position} is MSQXZ");
                    try
                    {
                        PluginLog.Debug($"Trying to pathfind");
                        var result = P.NavmeshManager.Pathfind(playerPos, marker.Position, false).Result;
                        if (result != null && result.Count > 0)
                        {
                            PluginLog.Debug($"Direct path found");
                            ret.Add(marker.Position);
                        }
                        else
                        {
                            PluginLog.Debug($"Direct path NOT found");
                            FindIngoringHeight();
                        }
                    }
                    catch (Exception e)
                    {
                        e.LogInfo();
                        FindIngoringHeight();
                    }

                    void FindIngoringHeight()
                    {
                        var alt = P.NavmeshManager.PointOnFloor(new(marker.Position.X, 1024, marker.Position.Z), false, 5);
                        PluginLog.Debug($"Trying point on floor: result = {alt}");
                        alt ??= P.NavmeshManager.NearestPoint(marker.Position, 5, 5);
                        PluginLog.Debug($"Trying nearest point: result = {alt}");
                        if (alt != null)
                        {
                            ret.Add(alt.Value);
                        }
                    }
                }
            }
            if (Environment.TickCount64 - time > 3000)
            {
                DuoLog.Error($"Pathfinding took {Environment.TickCount64 - time}ms > 3000, discarding");
            }
            else
            {
                Svc.Framework.RunOnFrameworkThread(() => callback(ret));
            }
        });
    }

    public static bool IsMTQ(this IGameObject x)
    {
        var id = x.Struct()->NamePlateIconId;
        if (Markers.MSQ.Contains(id) || Markers.ImportantSideProgress.Contains(id) || Markers.SideProgress.Contains(id))
        {
            return true;
        }
        else if (x.ObjectKind == ObjectKind.EventObj && x.IsTargetable && (Markers.EventObjWhitelist.Contains(x.BaseId) || Markers.EventObjNameWhitelist.ContainsIgnoreCase(x.Name.ToString())))
        {
            return true;
        }
        return false;
    }

    public static string GetGeneralActionName(int id)
    {
        return Svc.Data.GetExcelSheet<GeneralAction>().GetRow((uint)id).Name.ToString();
    }

    public static string GetMountName(int id)
    {
        // id 來自設定檔的 C.Mount，可能跨版本殘留、也可能是負數哨兵值。
        // 裸 GetRow 查無此列時 Lumina 會擲例外，而這裡的呼叫點包含設定視窗的 Draw
        // 路徑，一擲就把整個視窗弄不見；查不到時直接把 id 顯示出來讓使用者看得見。
        if(id < 0 || !Svc.Data.GetExcelSheet<Mount>().TryGetRow((uint)id, out var mount)) return $"?{id}";
        return mount.Singular.ExtractText();
    }

    public static bool CanFly() => C.EnableFlight && S.Memory.IsFlightProhibited(S.Memory.FlightAddr) == 0;

    public static bool ThrottleAutoInteract() => EzThrottler.Throttle("AutoInteract");
    public static bool ReThrottleAutoInteract() => EzThrottler.Throttle("AutoInteract", rethrottle: true);
    public static bool CheckThrottleAutoInteract() => EzThrottler.Check("AutoInteract");

    public static List<(int, int)> GetQuestArray()
    {
        var ret = new List<(int, int)>();
        var manager = QuestManager.Instance();
        for (var i = 0; i < manager->NormalQuests.Length; i++)
        {
            var q = manager->NormalQuests[i];
            ret.Add((q.QuestId, q.Sequence));
        }
        return ret;
    }
}
