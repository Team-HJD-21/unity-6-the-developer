// 제품별 공유 명령 목록과 Host의 최신 Player/대상 위치로 거리·생존·조작 상태를 검증합니다.
// 전력·잠금·냉각·강화 전이는 D 처리기가 다시 검사하며 위치가 없으면 허용하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class TurretInteractionAuthorization : ITurretCommandAuthorization
    {
        private readonly PlayerId _hostPlayer;
        private readonly IPlayerInteractionSource _players;
        private readonly Func<EntityId, BattlefieldPoint?> _turretPosition;
        private readonly Func<BattlefieldPoint?> _controlUnitPosition;
        private readonly HashSet<TurretCommandKind> _sharedCommands;
        private readonly double _maximumDistance;
        public TurretInteractionAuthorization(PlayerId hostPlayer, IPlayerInteractionSource players,
            Func<EntityId, BattlefieldPoint?> turretPosition, Func<BattlefieldPoint?> controlUnitPosition,
            double maximumDistance, IEnumerable<TurretCommandKind> sharedCommands)
        {
            if (hostPlayer.IsEmpty) throw new ArgumentException("A Host player is required.", nameof(hostPlayer));
            if (double.IsNaN(maximumDistance) || double.IsInfinity(maximumDistance) || maximumDistance <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _turretPosition = turretPosition ?? throw new ArgumentNullException(nameof(turretPosition));
            _controlUnitPosition = controlUnitPosition ?? throw new ArgumentNullException(nameof(controlUnitPosition));
            if (sharedCommands == null) throw new ArgumentNullException(nameof(sharedCommands));
            _sharedCommands = new HashSet<TurretCommandKind>();
            foreach (var command in sharedCommands)
            {
                if (!Enum.IsDefined(typeof(TurretCommandKind), command)) throw new ArgumentException("Unknown shared command.", nameof(sharedCommands));
                _sharedCommands.Add(command);
            }
            _hostPlayer = hostPlayer; _maximumDistance = maximumDistance;
        }
        public TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId verifiedIssuer)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (verifiedIssuer != _hostPlayer && !_sharedCommands.Contains(request.Kind))
                return TurretCommandPermission.Denied("SharedCommandNotAllowed", "This command is not enabled for remote participants.");
            if (!_players.TryCapture(request.Context.MatchId, verifiedIssuer, out var actor))
                return TurretCommandPermission.Denied("AuthorityActorUnavailable", "The Host has no verified current actor for this participant.");
            if (!actor.IsAlive) return TurretCommandPermission.Denied("PlayerDead");
            if (!actor.CanInteract) return TurretCommandPermission.Denied("InteractionBlocked");
            BattlefieldPoint? target = request.Kind == TurretCommandKind.StartAutoActivation ? _controlUnitPosition() : _turretPosition(request.TargetId);
            if (!target.HasValue) return TurretCommandPermission.Denied("InteractionTargetUnavailable");
            double dx = Math.Abs(actor.Position.X - target.Value.X), dy = Math.Abs(actor.Position.Y - target.Value.Y);
            // 큰 유효 double 좌표의 제곱 오버플로도 거리 허용으로 바뀌지 않게 정규화합니다.
            if (dx > _maximumDistance || dy > _maximumDistance ||
                (dx / _maximumDistance) * (dx / _maximumDistance) + (dy / _maximumDistance) * (dy / _maximumDistance) > 1)
                return TurretCommandPermission.Denied("OutOfInteractionRange");
            return TurretCommandPermission.Allowed;
        }
    }
}
