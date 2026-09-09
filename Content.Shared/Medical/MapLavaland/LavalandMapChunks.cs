using Robust.Shared.Serialization;
using Content.Shared.Pinpointer;

namespace Content.Shared.Medical.MapLavaland;
[Serializable, NetSerializable]
public class OldNavMap()
{
    public Dictionary<Vector2i, OldChunk> Chunks = new();

    public Vector2i pogr = new(0, 0);


    public void AddChunk(NavMapChunk chunk)
    {
        var oldchunk = new OldChunk(chunk, TimeSpan.Zero);
        if (Chunks.ContainsKey(chunk.Origin))
        {
            Chunks[chunk.Origin] = oldchunk;
        }
        else
        {
            Chunks.Add(chunk.Origin, oldchunk);
        }
    }
}
[Serializable, NetSerializable]
public sealed class OldChunk
{
    public NavMapChunk Chunk;

    public TimeSpan TimeWhenAdded = TimeSpan.FromSeconds(1);

    public OldChunk(NavMapChunk chunk, TimeSpan timeWhenAdded)
    {
        Chunk = new NavMapChunk(chunk.Origin);
        chunk.TileData.CopyTo(Chunk.TileData);
        Chunk.LastUpdate = chunk.LastUpdate;
        TimeWhenAdded = timeWhenAdded;
    }
}
