using Content.Shared.Medical.CrewMonitoring;
using Robust.Client.UserInterface;
using Content.Shared.Medical.MapLavaland;
namespace Content.Client.Medical.MapLavaland;

public sealed class MapLavalandBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MapLavaLandWindow? _menu;

    public MapLavalandBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
            {
                stationName = metaData.EntityName;
            }
        }

        _menu = this.CreateWindow<MapLavaLandWindow>();
        _menu.Set(stationName, gridUid);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case  MapLavaLandState st:
                _menu?.SetChunks(st, Owner);
                break;
        }
    }
}
