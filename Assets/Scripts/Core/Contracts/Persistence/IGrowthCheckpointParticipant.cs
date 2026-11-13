// 월드 복귀와 별개로 남길 실제 성장 원본입니다. 개인 actor 또는 Host 전장 소유자가 구현합니다.
// PersistenceKey는 roster 슬롯과 무관한 원본 이름이며 변경 통지는 실제 적용이 끝난 뒤 보냅니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public enum GrowthSaveUrgency { Coalesced, Immediate }
    public interface IGrowthCheckpointParticipant : ICheckpointParticipant
    {
        string PersistenceKey { get; }
        event Action<GrowthSaveUrgency> GrowthChanged;
    }
}
