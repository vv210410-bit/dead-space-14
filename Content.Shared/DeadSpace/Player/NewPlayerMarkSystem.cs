using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Content.Shared.Examine;
using Content.Shared.PDA;
namespace Content.Shared.DeadSpace.Player
{
    public sealed class NewPlayerMarkSystem : EntitySystem
    {
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly INetConfigurationManager _netCfg = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        }
        public void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
        {
            if (_inventorySystem.TryGetSlotEntity(ev.Mob, "id", out var idUid))
            {
                if (TryComp(idUid, out PdaComponent? pda) && _netCfg.IsCVarRegistered(CCCCVars.CCCCVars.NewPlayer.Name))
                {
                    pda.NewPlayer = _netCfg.GetClientCVar(ev.Player.Channel, CCCCVars.CCCCVars.NewPlayer);
                    Dirty(idUid.Value, pda);
                }
            }
        }
    }
}