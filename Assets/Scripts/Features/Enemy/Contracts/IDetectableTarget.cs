namespace TeamHJD.Game.Enemy.Contracts
{
    /// <summary>플레이어 감지자들의 등록 상태와 탐지 여부를 제공한다.</summary>
    public interface IDetectableTarget
    {
        /// <summary>현재 감지 중인 플레이어가 한 명 이상인지 반환한다.</summary>
        bool IsDetected { get; }

        /// <summary>이 대상을 감지 중인 플레이어 감지자를 등록한다.</summary>
        /// <param name="detectorId">감지 주체 컴포넌트의 인스턴스 ID.</param>
        void RegisterDetector(int detectorId);

        /// <summary>이 대상에 대한 해당 감지자의 등록을 해제한다.</summary>
        /// <param name="detectorId">등록할 때 사용한 감지 주체의 인스턴스 ID.</param>
        void UnregisterDetector(int detectorId);
    }
}
