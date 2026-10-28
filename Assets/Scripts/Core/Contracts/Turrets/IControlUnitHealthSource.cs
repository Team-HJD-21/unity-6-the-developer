// 실제 CU 체력 원본의 읽기와 파괴 사실만 Match 운영에 전달합니다.
// 피해 계산·회복·전력 규칙을 Core가 다시 소유하지 않습니다.
using System;

namespace TeamHJD.Game.Contracts
{
    public interface IControlUnitHealthSource : IControlUnitVitals
    {
        event Action Destroyed;
    }
}
