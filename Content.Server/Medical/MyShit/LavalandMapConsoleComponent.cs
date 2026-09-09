using System.Numerics;
using Content.Shared.Medical.MapLavaland;

namespace Content.Server.Medical.MyShit;

[RegisterComponent]
[Access(typeof(LavalandMapConsoleSystem))]
public sealed partial class LavalandMapConsoleComponent : Component
{
    // /// <summary>
    // ///     List of all currently connected sensors to this console.
    // /// </summary>
    public Dictionary<int, OldNavMap> OldNavMaps = new();

    public Dictionary<int, (Vector2, string)> VisitedGrids = new();
}