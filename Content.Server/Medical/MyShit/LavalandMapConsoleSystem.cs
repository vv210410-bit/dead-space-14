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

    public bool a = true;

    public List<(EntityUid, PhysicsComponent)> Entities = new();
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

        while (Entities.Count != 0)
        {
            _physics.SetBodyType(Entities.First().Item1, BodyType.Static);
            Dirty(Entities.First().Item1, Entities.First().Item2);
            Entities.RemoveAt(0);
        }
        while (maps.MoveNext(out var uid, out var map))
        {
            if (map.OldNavMaps.Count == 0)
            {
                var lookup = Get<EntityLookupSystem>();

                // Получаем координаты от сущности с компонентом Transform
                var transform = Transform(uid);
                var position = transform.Coordinates;

                // Ищем все сущности в радиусе 10 тайлов
                var entitiesInRange = lookup.GetEntitiesInRange(position, 500);

                // Фильтруем только стены
                foreach (var entity in entitiesInRange)
                {
                    if (HasComp<WallComponent>(entity))
                    {
                        EnsureComp<PhysicsComponent>(entity, out var physics);
                        _physics.SetBodyType(entity, BodyType.Dynamic);
                        Dirty(entity, physics);
                        Entities.Add((entity,physics));
                    }
                }
            }

            if (!_cell.HasActivatableCharge(uid))
                return;

            var navMaps = Get(uid);
            var xform = Transform(uid);
            var mappos = _transformSystem.ToMapCoordinates(xform.Coordinates).Position;

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
            foreach (var (navmap, pogr, gridId) in navMaps)
            {
                foreach (var (chunkOrigin, chunk) in navmap.Chunks)
                {
                    var chunkcenter = new Vector2(chunkOrigin.X * 8 + pogr.X + 4, chunkOrigin.Y * 8 + pogr.Y + 4);
                    if ((Math.Abs(mappos.X - (chunkcenter.X + 4)) <= 16 || Math.Abs(mappos.X - (chunkcenter.X - 4)) <= 16) &&
                    (Math.Abs(mappos.Y - (chunkcenter.Y + 4)) <= 16 || Math.Abs(mappos.Y - (chunkcenter.Y - 4)) <= 16))
                    {
                        if (!map.OldNavMaps.ContainsKey(gridId))
                        {
                            map.OldNavMaps.Add(gridId, new OldNavMap());
                        }

                        map.OldNavMaps[gridId].AddChunk(chunk);
                        map.OldNavMaps[gridId].pogr = pogr;
                    }
                }
            }
            if (xform.MapUid != null && map.OldNavMaps.ContainsKey(xform.MapUid.Value.Id))
            {
                var currentChunk = new Vector2i((int)Math.Floor(mappos.X / 8f),(int)Math.Floor(mappos.Y / 8f));
                for (int y = -1; y < 2; y++)
                {
                    for (int x = -1; x < 2; x++)
                    {
                        if (!map.OldNavMaps[xform.MapUid.Value.Id].Chunks.ContainsKey(new Vector2i(currentChunk.X + x, currentChunk.Y + y)))
                        {
                            var nullChunk = new NavMapChunk(new Vector2i(currentChunk.X + x, currentChunk.Y + y));
                            map.OldNavMaps[xform.MapUid.Value.Id].AddChunk(nullChunk);
                        }
                    }
                }
            }
            UpdateUserInterface(uid, map);
        }
    }

    public List<(NavMapComponent, Vector2i, int)> Get(EntityUid uid)
    {
        List<(NavMapComponent, Vector2i, int)> navMaps = new();
        List<Entity<MapGridComponent>>? grids = new();

        var xform = Transform(uid);
        _mapManager.FindGridsIntersecting(xform.MapID, new Box2(new Vector2i(0, 0) - 500, new Vector2i(0, 0) + 500), ref grids, approx: true, includeMap: true);
        for (int i = 0; i < grids.Count; i++)
        {
            var a = new NavMapComponent();
            TryComp(grids[i].Owner, out a);
            if (a != null && a != new NavMapComponent())
            {
                TransformComponent? transform = null;
                TryComp(grids[i].Owner, out transform);
                if (transform == null || transform.GridUid == null)
                    continue;

                var mappos = _transformSystem.ToMapCoordinates(transform.Coordinates);
                var pogr = new Vector2i((int)Math.Round(Math.Round(mappos.X, 1), MidpointRounding.AwayFromZero), (int)Math.Round(Math.Round(mappos.Y, 1), MidpointRounding.AwayFromZero));
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

        if (xform == null)
            return;
        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        _uiSystem.SetUiState(uid, LavaLandMapUIKey.Key, new MapLavaLandState(component.OldNavMaps, component.VisitedGrids));
    }
}
