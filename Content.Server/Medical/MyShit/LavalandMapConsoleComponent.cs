using System.Numerics;
using Content.Shared.Medical.MapLavaland;

namespace Content.Server.Medical.MyShit;

[RegisterComponent]
[Access(typeof(LavalandMapConsoleSystem))]
public sealed partial class LavalandMapConsoleComponent : Component
{
    /// <summary>
    /// Тут хранится Айди грида => Чанки грида. 
    /// Он хранит старые версии чанков, поэтому и назван  Old
    /// </summary>
    public Dictionary<int, OldNavMap> OldNavMaps = new();
    /// <summary>
    ///     Содержит информацию о посещенных гридах.
    ///     Айди грида => (Центр грида, имя)
    /// </summary>
    public Dictionary<int, (Vector2, string)> VisitedGrids = new();
}