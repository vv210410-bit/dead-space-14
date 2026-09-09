using System.Linq;
using System.Numerics;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.MapLavaland;

[Serializable, NetSerializable]
public enum LavaLandMapUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class MapLavaLandState : BoundUserInterfaceState
{
    public Dictionary<int, OldNavMap> OldNavMap;

    public List<(Vector2, string)> VisitedGrids;
    public MapLavaLandState(Dictionary<int, OldNavMap> oldNavMap, Dictionary<int, (Vector2, string)> visitedGrids)
    {
        OldNavMap = oldNavMap;
        VisitedGrids = visitedGrids.Values.ToList();
    }
}

