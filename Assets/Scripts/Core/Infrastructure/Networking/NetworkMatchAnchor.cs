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
