// 같은 Scene 조립부의 네트워크 준비·Client 읽기 복제·해제 접점을 모아 둡니다.
// 별도 객체나 상태 원본을 만들지 않는 InitialStageCompositionRoot의 partial 파일입니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {

        public void SetNetworkEncounterConsumer(MonoBehaviour consumer)
        {
            if (_ownsMatch || _playerActors != null) throw new InvalidOperationException("Set the consumer before Match composition.");
            if (consumer == null || !consumer.isActiveAndEnabled || !(consumer is IAppOwnedEncounterMatchConsumer))
                throw new ArgumentException("An active App-owned Encounter consumer is required.", nameof(consumer));
            _encounterConsumerBehaviour = consumer;
            var configurations = FindSceneBehaviours().OfType<IEncounterSceneConfiguration>().ToArray();
            if (configurations.Length > 1)
                throw new InvalidOperationException("The Match Scene cannot have multiple Encounter execution configurations.");
            // 현행 Enemy consumer는 자체 실행 대상을 구성합니다. Feature가 명시적 Scene 설정
            // 계약을 도입하면 그 연결을 사용하며, 기존 App 소유권·Host 검증은 그대로 유지합니다.
            if (configurations.Length == 1)
                configurations[0].ConfigureEncounter((IEncounterMatchConsumer)consumer);
        }

        /// <summary>준비 응답 전에 양쪽 Scene의 슬롯·콘텐츠·필수 제작 연결을 검사합니다.</summary>
        public void ValidateNetworkPreparation(NetworkMatchDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Config.StageId.Value != "initial-stage" || descriptor.Config.ContentVersion != "poc")
                throw new InvalidOperationException("The prepared Stage/content version is not supported by this Scene.");
            if (playerPrefab == null || battlefieldGridSettings == null || FindSceneCameraFollow() == null || RegionBinding == null ||
                GetComponent<TurretMatchBinding>() == null)
                throw new InvalidOperationException("Player, Grid, camera and region Scene references are required.");
            ValidateRegionAuthoring();
            PlayerStart[] starts = FindPlayerStarts();
            for (int slot = 0; slot < descriptor.Config.Players.Count; slot++)
                if (starts.Count(start => start.SlotIndex == slot) != 1)
                    throw new InvalidOperationException($"Expected exactly one active PlayerStart for slot {slot}.");
        }

        public void ComposeNetworkMatch(NetworkMatchCoordinator matches, bool isServer, ulong connectionId)
        {
            ValidateNetworkPreparation(matches?.Descriptor);
            if (isServer) { ComposeAuthority(_matchHost, matches, connectionId); return; }
            if (_playerActors != null) throw new InvalidOperationException("Client actors are already composed.");
            if (!matches.TryResolveParticipant(connectionId, out PlayerId playerId))
                throw new InvalidOperationException("The Client connection is not a participant.");
            _ownedMatchId = matches.Descriptor.Config.MatchId;
            try
            {
                _turretBinding = GetComponent<TurretMatchBinding>();
                _turretBinding.BindMatch(_ownedMatchId, () => false);
                _localActorConsumers = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .OfType<ILocalPlayerActorConsumer>().ToArray();
                foreach (var consumer in _localActorConsumers) consumer.PrepareLocalActor(_ownedMatchId, playerId);
                _playerActors = MatchPlayerActorScope.SpawnLocalPlayer(matches, connectionId,
                    new LocalPlayerActorSpawner(playerPrefab, actorParent), FindPlayerStarts());
                foreach (var consumer in _localActorConsumers)
                    consumer.BindLocalActor(_ownedMatchId, playerId, _playerActors.Actors[playerId]);
                BindLocalInput(_playerActors.Actors[playerId]);
                FindSceneCameraFollow().Bind(_playerActors.Actors[playerId].transform);
            }
            catch (Exception error)
            {
                try { ReleaseNetworkMatch(_ownedMatchId); }
                catch (Exception cleanupError) { throw new AggregateException("Client Scene setup and rollback failed.", error, cleanupError); }
                throw;
            }
        }

        /// <summary>네트워크 Scene 어댑터가 준비 세대를 검증한 Client 시계를 연결/해제합니다. 권위 시계는 교체하지 않습니다.</summary>
        public void BindTimeReplica(IMatchTime source)
        {
            if (source != null && (_ownsMatch || _ownedMatchId.IsEmpty || source.Snapshot.MatchId != _ownedMatchId))
                throw new InvalidOperationException("Bind a time replica only to its composed Client Match.");
            if (ReferenceEquals(_timeReplica, source)) return;
            var previous = _replicaField; _replicaField = null; _timeReplica = null;
            previous?.Dispose();
            if (source == null) return;
            _replicaField = new MatchFieldReplicaScope(source, GetFieldSimulationConsumers(), _playerActors.Actors.Values.Single());
            _timeReplica = source;
        }
        /// <summary>현재 descriptor의 로컬 슬롯 위치만 Client 필드 입력 경계에 반영합니다.</summary>
        public void ApplyReplicaLocation(MatchPlayerLocation location) => _replicaField?.ApplyLocation(location);

        public void BindRegionReplica(IRegionGridSource source)
        {
            _regionBinding = RegionBinding;
            _regionBinding.UnbindMatch();
            if (source != null) _regionBinding.BindReplica(source);
        }

        public void BindEnvironmentReplica(IMatchEnvironmentSource source)
        {
            IDisposable previous = _environmentScope; _environmentScope = null; _environmentReplica = null;
            previous?.Dispose();
            if (source == null) return;
            if (_ownsMatch || _ownedMatchId.IsEmpty) throw new InvalidOperationException("Bind environment replicas only to a composed Client Match.");
            _environmentScope = new MatchEnvironmentReplicaBinding(_ownedMatchId, source, _weatherSource, _timeOfDaySource);
            _environmentReplica = source;
        }

        public void ReleaseNetworkMatch(MatchId matchId)
        {
            if (_ownedMatchId != matchId) return;
            if (_ownsMatch) { _matchHost.TryEndMatch(matchId); return; }
            ReleaseOwnedBindings(matchId);
        }
    }
}
