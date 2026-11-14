// 게임 흐름이 Unity/네트워크 구현을 직접 참조하지 않고 App의 기존 전환·Match 조립을 호출하는 port입니다.
using TeamHJD.Game.Domain;
using System.Threading;
using System.Threading.Tasks;
namespace TeamHJD.Game.Contracts
{
    public interface IGameFlowHost
    {
        void PrepareMission(MissionEntryKind entry);
        string ActiveSceneKey { get; }
        string TravelRestriction { get; }
        bool CanLoad(string sceneKey);
        bool IsLoaded(string sceneKey);
        /// <summary>App의 기존 권한·최종 결과 전송·Match 종료·Scene 교체 순서를 사용합니다.</summary>
        Task TravelAsync(string sceneKey, CancellationToken cancellation);
        /// <summary>Scene 파일 로드 뒤 실제 Match 조립 성공을 확인합니다. 실패/취소는 게임 입장 성공이 아닙니다.</summary>
        Task WaitForMissionReadyAsync(string sceneKey, CancellationToken cancellation);
    }
}
