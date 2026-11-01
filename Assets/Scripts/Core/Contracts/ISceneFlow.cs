// 앱 구성부가 요청하는 Scene 전환 기능의 계약을 정의합니다.

using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface ISceneFlow
    {
        /// <summary>SceneFlowController가 전환 전에 제작된 Scene 키를 로드할 수 있는지 확인합니다.</summary>
        bool CanLoad(string sceneKey);
        /// <summary>같은 Scene을 중복 소유하지 않도록 현재 로드 상태를 조회합니다.</summary>
        bool IsLoaded(string sceneKey);
        /// <summary>진행 중 전환의 0~1 진행률입니다. NGO의 완료 대기는 NaN일 수 있습니다.</summary>
        float Progress { get; }
        /// <summary>SceneFlowController가 현재 Scene을 유지한 채 대상 Scene을 추가로 로드합니다.</summary>
        /// <remarks>취소/정리는 구현의 commit 규칙을 따릅니다. Feature는 App의 전환 조율 경계에 요청합니다.</remarks>
        Task LoadAsync(string sceneKey, CancellationToken cancellationToken);
        /// <summary>SceneFlowController가 게임 흐름의 Match 정리 이후 Scene 교체를 실행합니다.</summary>
        /// <remarks>SDK 전환 시작 후 취소 가능 여부는 어댑터 계약을 따릅니다. Task 완료는 보상 지급을 뜻하지 않습니다.</remarks>
        Task ReplaceAsync(string sceneKey, CancellationToken cancellationToken);
        /// <summary>SceneFlowController가 소유한 추가 Scene을 해제하고 완료를 기다립니다.</summary>
        Task UnloadAsync(string sceneKey, CancellationToken cancellationToken);
    }
}
