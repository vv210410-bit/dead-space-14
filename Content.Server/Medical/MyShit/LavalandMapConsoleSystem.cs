using System.Linq;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.MapLavaland;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared.Wall;
using Robust.Shared.Physics.Systems;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mind.Filters;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Players;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;


namespace Content.Server.Medical.MyShit;

public sealed class LavalandMapConsoleSystem : EntitySystem
{

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly IMapManager _mapManager = default!;

    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const float UpdateRate = 3f;
    private float _updateDiff;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMapConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // check update rate
        _updateDiff += frameTime;
        if (_updateDiff < UpdateRate)
            return;
        _updateDiff -= UpdateRate;

        var maps = EntityQueryEnumerator<LavalandMapConsoleComponent>();

        while (maps.MoveNext(out var uid, out var map))
        {
            if (!_cell.HasActivatableCharge(uid))
                return;

            var xform = Transform(uid);
            var mapPos = _transformSystem.ToMapCoordinates(xform.Coordinates).Position;

            if (xform.MapUid == null)
                return;

            List<Entity<MapGridComponent>> grids = new();
            _mapManager.FindGridsIntersecting(xform.MapID, new Box2(new Vector2i(0, 0) - 500, new Vector2i(0, 0) + 500), ref grids, approx: true, includeMap: true);
            var navMaps = Get(grids);

            //Добавляем грид в посещенные
            if (xform.GridUid != null && !map.VisitedGrids.ContainsKey(xform.GridUid.Value.Id))
            {
                TryComp(xform.GridUid, out MapGridComponent? grid);
                if (grid != null)
                {
                    var res = GetGrid(grid);
                    if (res != null)
                        map.VisitedGrids.Add(xform.GridUid.Value.Id, res.Value);
                }
            }

            //Записываем информацию о ближайших чанках в компонент. 
            foreach (var (navmap, pogr, gridId) in navMaps)
            {
                //переводем позицию игрока в чанк.
                var posToChunk = new Vector2i((int)Math.Floor((mapPos.X + pogr.X) / 8), (int)Math.Floor((mapPos.Y + pogr.Y) / 8));
                posToChunk = new Vector2i(0,0);

                if (!map.OldNavMaps.ContainsKey(gridId))
                {
                    map.OldNavMaps.Add(gridId, new OldNavMap());
                }
                map.OldNavMaps[gridId].Pogr = pogr;

                foreach (var chunk in navmap.Chunks)
                {
                    map.OldNavMaps[gridId].AddChunk(chunk.Value);
                }
            }

            // добавляем "пустые чанки,чтобы потом они тоже были помечены, как посещенные
            if (xform.MapUid != null && map.OldNavMaps.ContainsKey(xform.MapUid.Value.Id))
            {
                var posToChunk = new Vector2i((int)Math.Floor(mapPos.X / 8f), (int)Math.Floor(mapPos.Y / 8f));
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (!map.OldNavMaps[xform.MapUid.Value.Id].Chunks.ContainsKey(new Vector2i(posToChunk.X + x, posToChunk.Y + y)))
                        {
                            var nullChunk = new NavMapChunk(new Vector2i(posToChunk.X + x, posToChunk.Y + y));
                            map.OldNavMaps[xform.MapUid.Value.Id].AddChunk(nullChunk);
                        }
                    }
                }
            }

            var gridsId = grids.Select(p => p.Owner.Id);

            //Если грид исчез, то убираем его из списка. Например: Шатл
            var keys = map.VisitedGrids.Keys.ToList();
            foreach (var key in keys)
            {
                if (!gridsId.Contains(key))
                    map.VisitedGrids.Remove(key);
            }

            //Если грид исчез, то убираем его из списка. Например: Шатл
            keys = map.OldNavMaps.Keys.ToList();
            foreach (var key in keys)
            {
                if (!gridsId.Contains(key))
                    map.OldNavMaps.Remove(key);
            }

            UpdateUserInterface(uid, map);
        }
    }

    /// <summary>
    ///    Получаем все NavMap;
    ///    <return>Возвращает (NavMap, погрешность, айди грида) </return>
    /// </summary>
    public List<(NavMapComponent, Vector2, int)> Get(List<Entity<MapGridComponent>> grids)
    {
        List<(NavMapComponent, Vector2, int)> navMaps = new();
        for (int i = 0; i < grids.Count; i++)
        {
            var a = new NavMapComponent();
            TryComp(grids[i].Owner, out a);
            if (a != null && a != new NavMapComponent())
            {
                TransformComponent? transform = Transform(grids[i].Owner);
                if (transform.GridUid == null)
                    continue;

                var mappos = _transformSystem.ToMapCoordinates(transform.Coordinates);
                var pogr = new Vector2(mappos.X, mappos.Y);
                foreach (var chunk in a.Chunks)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        if (chunk.Value.TileData[x] != 0)
                        {
                            var gsdgssgd = 0;
                        }
                    }
                }
                navMaps.Add((a, pogr, transform.GridUid.Value.Id));
            }
        }

        return navMaps;
    }

    public (Vector2, string)? GetGrid(MapGridComponent grid)
    {
        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        var labelText = MetaData(grid.Owner).EntityName;

        PhysicsComponent? gridBody = null;
        bodyQuery.TryGetComponent(grid.Owner, out gridBody);
        if (gridBody == null)
            return null;

        // yes 1.0 scale is intended here.

        var xform = Transform(grid.Owner);
        var gridScaledPosition = _transformSystem.ToMapCoordinates(xform.Coordinates).Position;

        gridScaledPosition += gridBody.LocalCenter;

        return (gridScaledPosition, labelText);
    }
    private void OnUIOpened(EntityUid uid, LavalandMapConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }


    private void UpdateUserInterface(EntityUid uid, LavalandMapConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.IsUiOpen(uid, LavaLandMapUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform == null || xform.MapUid == null)
            return;
        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        _uiSystem.SetUiState(uid, LavaLandMapUIKey.Key, new MapLavaLandState(component.OldNavMaps, component.VisitedGrids));
    }
}
