// Host가 생성·갱신·제거하는 최소 Match-scope NetworkObject의 읽기 전용 투영입니다.
// 설정/지역 원본은 Core에 남기고 동일 Match와 논리 ID의 실제 객체 수명을 확인합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEngine;

namespace TeamHJD.Game.Infrastructure
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatchAnchor : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkMatchAnchorState> _state = new NetworkVariable<NetworkMatchAnchorState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkMatchAnchorState State => _state.Value;
        private readonly NetworkVariable<NetworkMatchTimeState> _time = new NetworkVariable<NetworkMatchTimeState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkMatchTimeState TimeState => _time.Value;
        private readonly NetworkVariable<NetworkMatchTimeState> _fieldTime = new NetworkVariable<NetworkMatchTimeState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkMatchTimeState FieldTimeState => _fieldTime.Value;
        /// <summary>현재 서버 조립부가 정지·배속·복원으로 변경된 시간 기준을 게시합니다. SDK 시계는 변경하지 않습니다.</summary>
        public void ApplyAuthorityTime(NetworkMatchDescriptor descriptor, MatchTimeSnapshot time, MatchTimeSnapshot fieldTime,
            long presenceRevision = 0, byte spaceshipMask = 0)
        {
            if (!IsSpawned || !IsServer) throw new InvalidOperationException("Only the spawned server anchor may publish time.");
            if (descriptor == null || time == null || fieldTime == null || time.MatchId != descriptor.Config.MatchId ||
                fieldTime.MatchId != time.MatchId || fieldTime.Epoch != time.Epoch ||
                State.MatchId.ToString() != time.MatchId.Value || State.DescriptorId.ToString() != descriptor.DescriptorId.ToString("N"))
                throw new ArgumentException("Time must belong to this prepared anchor.");
            _time.Value = NetworkMatchTimeState.From(descriptor.DescriptorId, time, presenceRevision, spaceshipMask);
            _fieldTime.Value = NetworkMatchTimeState.From(descriptor.DescriptorId, fieldTime);
        }
        public TeamHJD.Game.Domain.EntityId LogicalEntityId => State.DescriptorId.IsEmpty ? default :
            new TeamHJD.Game.Domain.EntityId("match-anchor/" + State.DescriptorId.ToString());

        public void ApplyAuthorityState(NetworkMatchDescriptor descriptor, RegionGridSnapshot region)
        {
            if (!IsSpawned || !IsServer) throw new InvalidOperationException("Only the spawned server anchor may change state.");
            if (descriptor == null || region == null || descriptor.Config.MatchId != region.MatchId)
                throw new ArgumentException("Anchor state must belong to the prepared Match.");
            if (!State.MatchId.IsEmpty && (State.MatchId.ToString() != descriptor.Config.MatchId.Value ||
                State.DescriptorId.ToString() != descriptor.DescriptorId.ToString("N")))
                throw new InvalidOperationException("A spawned anchor cannot be reassigned to another Match.");
            _state.Value = new NetworkMatchAnchorState
            {
                MatchId = descriptor.Config.MatchId.Value, DescriptorId = descriptor.DescriptorId.ToString("N"),
                StageId = descriptor.Config.StageId.Value, Seed = descriptor.Config.RandomSeed,
                RegionGridId = region.GridId.ToString("N"),
                RegionRevision = region.Revision, CapturedCells = region.CapturedCellCount, RegionMode = (int)region.Mode
            };
        }
    }
}
