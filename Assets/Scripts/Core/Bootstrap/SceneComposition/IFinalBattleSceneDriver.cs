// Stage에 지정된 실제 Boss Scene 준비 구현의 연결 계약입니다.
// Core가 저장/Scene 로드/참가자 원위치 복구를 맡고, 구현자는 보스·도착·연출·소유 자원 정리를 맡습니다.
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public interface IFinalBattleSceneDriver
    {
        string ScenePath { get; }
        /// <summary>같은 Match로 로드된 실제 Scene에 참가자를 배치하고 보스·연출이 준비된 뒤 실제 도착 명단을 반환합니다.</summary>
        /// <remarks>AuthorityActors는 현재 실제 객체이며 보스는 MatchTime/GameplayCancellation을 사용합니다. 전환 취소 토큰을 전투 수명으로 쓰지 않습니다.</remarks>
        Task<FinalBattleSceneReady> PrepareAsync(InitialStageCompositionRoot stage, CancellationToken cancellation);
        /// <summary>실패/안전장치 복귀 시 소유한 보스·카메라·연출 자원을 정리합니다. Prepare 전의 부분 로드에도 반복 호출할 수 있어야 합니다.</summary>
        Task RollbackAsync(CancellationToken cancellation);
        /// <summary>Match 종료 시 보스·카메라·연출의 Match 바인딩을 즉시 닫습니다. Scene 로드/해제는 하지 않으며 반복 호출에 안전해야 합니다.</summary>
        void StopMatch();
    }
}
