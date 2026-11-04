// 모든 Feature의 실제 actor 재생성이 끝난 뒤 논리 ID의 대상 참조를 연결하는 선택 계약입니다.
// 백업 복구도 전체 재생성이 끝난 뒤 다시 연결하며, 이미 폐기된 Unity instance를 재사용하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointReferenceRestoreStep : ICheckpointRestoreStep
    {
        /// <summary>모든 Apply 이후, 결과 게시 전에 저장된 논리 참조를 현재 actor에 연결합니다. 누락/충돌은 예외로 거부합니다.</summary>
        void ResolveReferences();
        /// <summary>모든 Rollback 이후, 백업 세계의 논리 참조를 다시 연결합니다. Apply 전 실패한 단계에도 호출될 수 있습니다.</summary>
        void ResolveRollbackReferences();
    }
}
