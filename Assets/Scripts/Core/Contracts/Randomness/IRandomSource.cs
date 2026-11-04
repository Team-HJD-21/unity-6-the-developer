// Feature가 사용하는 범용 난수 함수 계약입니다.
// 실제 상태·seed·권한은 Core가 소유하며 Unity 전역 Random 상태를 사용하지 않습니다.

namespace TeamHJD.Game.Contracts
{
    public interface IRandomSource
    {

        /// <summary>현재 수명과 권한에서 난수를 소비할 수 있는지 확인합니다.</summary>
        bool IsAvailable { get; }

        /// <summary>32비트 전체 범위의 난수를 반환합니다. 결정적 알고리즘 검증에도 사용합니다.</summary>
        uint NextUInt32();

        /// <summary>minInclusive 이상 maxExclusive 미만의 정수를 편향 없이 선택합니다.</summary>
        /// <remarks>minInclusive는 maxExclusive보다 작아야 합니다. int 전체 폭도 허용합니다.</remarks>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>0 이상 1 미만의 실수를 반환합니다.</summary>
        float NextFloat();

        /// <summary>유한한 minInclusive 이상 maxExclusive 미만의 실수를 반환합니다.</summary>
        float NextFloat(float minInclusive, float maxExclusive);

        /// <summary>0 이상 1 미만의 53비트 해상도 실수를 반환합니다.</summary>
        double NextDouble();

        /// <summary>유한한 minInclusive 이상 maxExclusive 미만의 실수를 반환합니다.</summary>
        double NextDouble(double minInclusive, double maxExclusive);
    }
}
