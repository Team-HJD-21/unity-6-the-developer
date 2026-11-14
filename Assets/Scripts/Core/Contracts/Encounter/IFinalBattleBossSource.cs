// 최종 Scene의 실제 Enemy 원본과 Core의 도착·연출 조율을 연결합니다.
// 보스 종류/생성 방식은 Enemy가 소유하며 Core는 실제 식별자와 수명 정리만 사용합니다.
using System.Threading;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IFinalBattleBossSource
    {
        /// <summary>Core driver가 전투용 공용 시각을 전달합니다. 준비 정지 중 생성하되 실제 전투는 같은 시각의 재개를 따릅니다.</summary>
        EntityId PrepareFinalBoss(IMatchTime time, CancellationToken gameplay);
        /// <summary>실패/복귀/Match 종료 때 이 원본이 만든 개체와 시간 연결만 정리합니다. 반복 호출할 수 있습니다.</summary>
        void ReleaseFinalBoss();
    }
}
