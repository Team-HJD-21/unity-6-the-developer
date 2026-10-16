// 지역 Grid의 확정 상태를 Feature의 사용 가능 여부로 해석합니다.
// 동적 모드는 활성화가 점령의 입력이므로 점령 결과로 터렛을 다시 잠그지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IRegionAccessPolicy
    {
        IRegionGridSource Regions { get; }
        bool CanUseAt(BattlefieldPoint position);
    }
}
