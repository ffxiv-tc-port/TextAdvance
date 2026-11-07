using ECommons.EzIpcManager;

namespace TextAdvance.Navmesh;
public class NavmeshManager
{
    [EzIPC("Nav.%m")] public readonly Func<bool> IsReady;
    [EzIPC("Nav.%m")] public readonly Func<float> BuildProgress;
    [EzIPC("Nav.%m")] public readonly Func<bool> Reload;
    [EzIPC("Nav.%m")] public readonly Func<bool> Rebuild;
    /// <summary>
    /// Vector3 from, Vector3 to, bool fly
    /// </summary>
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind;

    [EzIPC("SimpleMove.%m")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo;
    [EzIPC("SimpleMove.%m")] public readonly Func<bool> PathfindInProgress;

    [EzIPC("Path.%m")] public readonly Action Stop;
    [EzIPC("Path.%m")] public readonly Func<bool> IsRunning;
    /// <summary>剩下的路徑點，最後一個＝終點；端點不存在或 vnavmesh 沒裝時為 null。</summary>
    [EzIPC("Path.%m")] public readonly Func<List<Vector3>> ListWaypoints;

    /// <summary>
    /// Vector3 p, float halfExtentXZ, float halfExtentY
    /// </summary>
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPoint;
    /// <summary>
    /// Vector3 p, bool allowUnlandable, float halfExtentXZ
    /// </summary>
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, bool, float, Vector3?> PointOnFloor;

    public NavmeshManager()
    {
        EzIPC.Init(this, "vnavmesh", SafeWrapper.AnyException);
    }
}