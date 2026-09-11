using Robust.Shared.Serialization;
using Content.Shared.Pinpointer;
using System.Numerics;

namespace Content.Shared.Medical.MapLavaland;
[Serializable, NetSerializable]
/// <summary>
///    Хранит в себе чанки и погрешность для них.
///    Нужен потому-что обычный NavMap хранит слишком много
///    лишней для нас информации.
/// </summary>
public class OldNavMap()
{
    public Dictionary<Vector2i, NavMapChunk> Chunks = new();

    /// <summary>
    ///    насколько нужно сместить координаты тайла в чанке,
    ///    чтобы оно совпало с позицией в мире.
    /// </summary>
    public Vector2 Pogr = new(0, 0);


    public void AddChunk(NavMapChunk chunk)
    {
        var oldChunk = new NavMapChunk(chunk.Origin);
        oldChunk.TileData.CopyTo(chunk.TileData);
        oldChunk.LastUpdate = chunk.LastUpdate;
        if (Chunks.ContainsKey(chunk.Origin))
        {
            Chunks[chunk.Origin] = oldChunk;
        }
        else
        {
            Chunks.Add(chunk.Origin, oldChunk);
        }
    }
}
