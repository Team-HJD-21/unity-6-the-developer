// 기존 서버 소유 Match anchor를 사용해 Host 기준 변경과 Client 읽기 시각을 연결합니다.
// 연결/NetworkObject 생성은 기존 Scene binding에 남기고 별도의 전역 시간 Manager를 만들지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class MatchTimeSceneReplication : IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NetworkMatchDescriptor _descriptor;
        private readonly InitialStageCompositionRoot _scene;
        private readonly MatchTimeReplica _replica;
        private readonly MatchTimeReplica _fieldReplica;
        private NetworkMatchAnchor _anchor;
        private bool _published, _disposed;
        private long _presenceRevision = -1;
        public MatchTimeSceneReplication(NetworkManager manager, NetworkMatchDescriptor descriptor,
            InitialStageCompositionRoot scene, NetworkMatchAnchor authorityAnchor)
        {
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _scene = scene != null ? scene : throw new ArgumentNullException(nameof(scene)); _anchor = authorityAnchor;
            if (!manager.IsServer)
            {
                var timestamp = new NgoTimestampSource(manager);
                _replica = new MatchTimeReplica(descriptor.Config.MatchId, descriptor.DescriptorId, timestamp);
                _fieldReplica = new MatchTimeReplica(descriptor.Config.MatchId, descriptor.DescriptorId, timestamp);
            }
        }
        public void Pump()
        {
            if (_disposed) return;
            if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress)
            { _replica?.Pump(false); _fieldReplica?.Pump(false); return; }
            if (_manager.IsServer)
            {
                var time = _scene.MatchTime;
                var field = _scene.FieldTime;
                if (_anchor == null || !_anchor.IsSpawned || time == null || field == null) return;
                var presence = _scene.MatchPresence;
                if (_anchor.TimeState.MatchId.IsEmpty || _anchor.FieldTimeState.MatchId.IsEmpty ||
                    _anchor.TimeState.Revision != time.Snapshot.Revision ||
                    _anchor.FieldTimeState.Revision != field.Snapshot.Revision ||
                    _anchor.TimeState.PresenceRevision != (presence?.Revision ?? 0))
                {
                    byte mask = 0;
                    for (int i = 0; i < _descriptor.Config.Players.Count; i++)
                        if (presence != null && presence.Locations.TryGetValue(_descriptor.Config.Players[i].PlayerId, out var location) &&
                            location == TeamHJD.Game.Domain.MatchPlayerLocation.Spaceship) mask |= (byte)(1 << i);
                    _anchor.ApplyAuthorityTime(_descriptor, time.Snapshot, field.Snapshot, presence?.Revision ?? 0, mask);
                }
                return;
            }
            if (_anchor == null || !_anchor.IsSpawned)
                _anchor = _manager.SpawnManager.SpawnedObjectsList.Select(item => item.GetComponent<NetworkMatchAnchor>())
                    .FirstOrDefault(item => item != null && item.State.DescriptorId.ToString() == _descriptor.DescriptorId.ToString("N") &&
                        item.State.MatchId.ToString() == _descriptor.Config.MatchId.Value);
            bool hasAnchor = _anchor != null && _anchor.IsSpawned;
            TeamHJD.Game.Domain.MatchTimeSnapshot commonTime = null, fieldTime = null;
            Guid descriptor = default, fieldDescriptor = default;
            bool valid = hasAnchor && _anchor.TimeState.TryRead(out descriptor, out commonTime) &&
                _anchor.FieldTimeState.TryRead(out fieldDescriptor, out fieldTime) &&
                descriptor == _descriptor.DescriptorId && fieldDescriptor == descriptor &&
                commonTime.MatchId == _descriptor.Config.MatchId && fieldTime.MatchId == commonTime.MatchId &&
                commonTime.Epoch == fieldTime.Epoch && commonTime.Rate == fieldTime.Rate &&
                commonTime.IsAvailable == fieldTime.IsAvailable && (!commonTime.IsPaused || fieldTime.IsPaused);
            if (valid)
            {
                _replica.TryApply(_descriptor.DescriptorId, commonTime, fromAuthority: true);
                _fieldReplica.TryApply(_descriptor.DescriptorId, fieldTime, fromAuthority: true);
            }
            _replica.Pump(valid); _fieldReplica.Pump(valid);
            if (!_published && valid && _replica.HasSnapshot && _fieldReplica.HasSnapshot)
            { _scene.BindTimeReplica(_replica, _fieldReplica); _published = true; }
            if (_published && valid && _anchor.TimeState.PresenceRevision > _presenceRevision)
            {
                _presenceRevision = _anchor.TimeState.PresenceRevision;
                if (!_descriptor.TryGetSlot(_manager.LocalClientId, out int slot)) throw new InvalidOperationException("The Client has no prepared player slot.");
                _scene.ApplyReplicaLocation((_anchor.TimeState.SpaceshipMask & (1 << slot)) != 0 ?
                    TeamHJD.Game.Domain.MatchPlayerLocation.Spaceship : TeamHJD.Game.Domain.MatchPlayerLocation.Field);
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try { if (_published && _scene != null) _scene.BindTimeReplica(null, null); }
            finally { _replica?.Dispose(); _fieldReplica?.Dispose(); _anchor = null; }
        }
    }
}
