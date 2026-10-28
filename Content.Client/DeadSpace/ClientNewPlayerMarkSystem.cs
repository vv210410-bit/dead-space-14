using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Client.Player;
using Robust.Shared.Player;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
namespace Content.Client.DeadSpace
{
    public sealed class ClientNewPlayerMarkSystem : EntitySystem
    {
        // [Dependency] private readonly IServerDbManager _dbManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly ISharedPlaytimeManager _playTime = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ => JoinLobby());
        }

        private void JoinLobby()
        {
            if (_playerManager.LocalSession == null)
                return;
            var b = _playTime.GetPlayTimes(_playerManager.LocalSession);
            if (b.TryGetValue("Overall", out var time) && time.TotalHours < CCVars.NewPlayerThreshold.DefaultValue)
                _cfg.SetCVar(CCCCVars.NewPlayer, true);

        }
    }
}