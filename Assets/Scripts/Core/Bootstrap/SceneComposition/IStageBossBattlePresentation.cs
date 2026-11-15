// Stage1 진행 소유자가 로컬 컷씬·보스 HUD·승리 연출을 요청하는 표시 경계입니다.
// Scene 로드·보스 생성·피해 판정·안전장치 저장은 이 구현이 소유하지 않습니다.
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public interface IStageBossBattlePresentation
    {
        Task PlayEntranceAsync(CancellationToken cancellation);
        Task ShowBattleIntroAsync(GameObject boss, string displayName, CancellationToken cancellation);
        Task PlayVictoryAsync(string message, float holdSeconds, CancellationToken cancellation);
        void ResetBattle();
    }
}
