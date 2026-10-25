// 한 네트워크 Match의 D 명령·Host 검증·로컬/원격 gateway를 조립합니다.
// 접속 전송은 App에서 빌리며, 실제 Player/터렛 생성·복제는 Feature에 남깁니다.
// 서버 actor 제공자/요청 구독은 이 scope 종료에 닫고 D 개체를 임의 파괴하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class TurretCommandSceneScope : IDisposable
    {
        private readonly NetworkMatchCoordinator _matches;
        private readonly INetworkSession _network;
        private readonly TurretMatchBinding _turrets;
        private readonly Transform _controlUnit;
        private readonly IControlUnitInteractionTarget _controlTarget;
        private readonly MonoBehaviour _controlTargetBehaviour;
        private readonly PlayerInteractionRegistry _actors;
        private readonly List<PlayerInteractionActorBinding> _localActors = new List<PlayerInteractionActorBinding>();
        private TurretCommandGateway _gateway;
        private TurretPresentationSceneSource _hostPresentation;
        private TurretPresentationReplica _clientPresentation;
        private TurretPresentationReplication _presentationReplication;
        private readonly List<ITurretCommandConsumer> _consumers = new List<ITurretCommandConsumer>();
        private float _nextPresentationSample;
        private bool _disposed;
        public MatchId MatchId { get; }
        public Guid DescriptorId { get; }
        public ITurretCommandRequester Requester => _gateway;
        public ITurretPresentationSource Presentation => _hostPresentation ?? (ITurretPresentationSource)_clientPresentation;
        private bool IsRunning() => !_disposed && _matches.Phase == NetworkMatchPhase.Running &&
            _matches.Descriptor?.DescriptorId == DescriptorId && _turrets.BoundMatchId == MatchId;
        private bool IsAuthority() => IsRunning() && _network.IsServer;
        private PlayerId? Resolve(ulong connection) => IsRunning() && _matches.TryResolveParticipant(connection, out var player) ? player : (PlayerId?)null;
        private bool IsParticipant(PlayerId player) => IsRunning() && _matches.Descriptor.Participants.Any(item => item.PlayerId == player);
        public TurretCommandSceneScope(NetworkMatchCoordinator matches, INetworkSession network, ITurretCommandTransport transport,
            ITurretPresentationTransport presentationTransport,
            TurretMatchBinding turrets, Transform controlUnit, IReadOnlyDictionary<PlayerId, GameObject> localActors,
            double interactionDistance)
        {
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _turrets = turrets != null ? turrets : throw new ArgumentNullException(nameof(turrets));
            _controlUnit = controlUnit != null ? controlUnit : throw new ArgumentNullException(nameof(controlUnit));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (presentationTransport == null) throw new ArgumentNullException(nameof(presentationTransport));
            if (matches.Phase != NetworkMatchPhase.Running || matches.Descriptor == null ||
                !matches.TryResolveParticipant(network.LocalConnectionId, out var localPlayer))
                throw new InvalidOperationException("Compose turret commands for the current running participant.");
            MatchId = matches.Descriptor.Config.MatchId; DescriptorId = matches.Descriptor.DescriptorId;
            if (turrets.BoundMatchId != MatchId) throw new InvalidOperationException("The D identity scope is not ready.");
            ulong hostConnection = matches.Descriptor.Participants.Single(item => item.IsHost).ConnectionId;
            try
            {
                if (network.IsServer)
                {
                    _actors = new PlayerInteractionRegistry(MatchId, matches.Descriptor.Participants.Select(item => item.PlayerId));
                    IControlUnitInteractionTarget[] targets = controlUnit.GetComponents<MonoBehaviour>().OfType<IControlUnitInteractionTarget>().ToArray();
                    if (targets.Length != 1) throw new InvalidOperationException("Provide one CU interaction target.");
                    _controlTarget = targets[0];
                    _controlTargetBehaviour = (MonoBehaviour)_controlTarget;
                    if (localActors == null || !localActors.ContainsKey(localPlayer)) throw new InvalidOperationException("Provide the current Host actor.");
                    foreach (var actor in localActors) _localActors.Add(RegisterAuthorityActor(actor.Key, actor.Value));
                    // 사용자 확정 정책: 이 여섯 공용 명령 모두 참가자가 요청하고 Host가 검증합니다.
                    var shared = new[] { TurretCommandKind.SetActivation, TurretCommandKind.StartAutoActivation,
                        TurretCommandKind.UpgradeLevel, TurretCommandKind.DowngradeLevel,
                        TurretCommandKind.ApplyUpgrade, TurretCommandKind.DowngradeUpgrade };
                    var policy = new TurretInteractionAuthorization(localPlayer, _actors, CaptureTurretPosition,
                        CaptureControlPosition, interactionDistance, shared);
                    turrets.BindCommands(IsAuthority, IsRunning, IsParticipant, policy);
                }
                _gateway = new TurretCommandGateway(MatchId, DescriptorId, localPlayer, hostConnection, network,
                    transport, IsRunning, Resolve, network.IsServer ? turrets.Commands : null);
                if (network.IsServer)
                {
                    _hostPresentation = new TurretPresentationSceneSource(MatchId, DescriptorId, turrets, controlUnit, IsAuthority);
                    _gateway.AuthorityResultProduced += _hostPresentation.ObserveCommand;
                    _hostPresentation.Refresh();
                }
                else _clientPresentation = new TurretPresentationReplica(MatchId, DescriptorId);
                _presentationReplication = new TurretPresentationReplication(network, matches, presentationTransport, _hostPresentation, _clientPresentation);
                foreach (var consumer in turrets.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).OfType<ITurretCommandConsumer>())
                {
                    _consumers.Add(consumer);
                    consumer.BindTurretCommands(MatchId, localPlayer, _gateway, Presentation);
                }
            }
            catch { Dispose(); throw; }
        }
        /// <summary>B가 Host에 생성한 실제 원격 actor를 등록하고 교체/Despawn 때 반환 lease를 해제합니다.</summary>
        public PlayerInteractionActorBinding RegisterAuthorityActor(PlayerId playerId, GameObject actor)
        {
            if (!IsAuthority() || _actors == null) throw new InvalidOperationException("Only the current Host may register authoritative actors.");
            if (actor == null || actor.scene != _turrets.gameObject.scene) throw new ArgumentException("The actor must belong to this Match scene.", nameof(actor));
            return new PlayerInteractionActorBinding(_actors, playerId, actor, IsAuthority);
        }
        private BattlefieldPoint? CaptureTurretPosition(EntityId id)
        {
            if (!IsAuthority() || !_turrets.TryResolveLocalId(id, out int localId) ||
                !TurretInstanceRegistry.TryGet(localId, out var turret) || turret == null || !turret.isActiveAndEnabled) return null;
            Vector3 point = turret.transform.position;
            if (float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y)) return null;
            return new BattlefieldPoint(point.x, point.y);
        }
        private BattlefieldPoint? CaptureControlPosition() => IsAuthority() && _controlUnit != null &&
            _controlTargetBehaviour != null && _controlTargetBehaviour.isActiveAndEnabled &&
            _controlTarget.TryCaptureInteractionPosition(out var point) ? point : (BattlefieldPoint?)null;
        public void Pump()
        {
            if (_disposed) return;
            if (_network.IsServer && (_controlUnit == null || !_turrets.Controller.isActiveAndEnabled || !_turrets.Controller.HasWriteAuthority))
                throw new InvalidOperationException("The Match lost its D control scope.");
            if (_hostPresentation != null && Time.unscaledTime >= _nextPresentationSample)
            { _nextPresentationSample = Time.unscaledTime + .1f; _hostPresentation.Refresh(); }
            _gateway?.Pump();
            _presentationReplication?.Pump();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            List<Exception> errors = null;
            _presentationReplication?.Dispose(); _presentationReplication = null;
            foreach (var consumer in _consumers)
            { try { consumer.UnbindTurretCommands(MatchId); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); } }
            _consumers.Clear();
            _gateway?.Dispose(); _gateway = null;
            try { _hostPresentation?.Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            try { _clientPresentation?.Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            foreach (var actor in _localActors) actor.Dispose();
            _localActors.Clear(); _actors?.Dispose();
            if (errors != null) throw new AggregateException("Turret command presentation cleanup failed.", errors);
        }
    }
}
