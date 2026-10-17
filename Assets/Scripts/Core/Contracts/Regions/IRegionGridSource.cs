// 게임과 도구가 같은 지역 Grid의 확정 결과를 읽는 경계입니다.
// 조회자는 상태 변경이나 정책 실행 권한을 얻지 않습니다.
using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IRegionGridSource
    {
        RegionGridSnapshot Snapshot { get; }
        event Action<RegionGridSnapshot> Changed;
    }
}
