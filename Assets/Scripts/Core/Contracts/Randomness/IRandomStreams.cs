// 여러 Feature의 난수를 같은 API로 제공하되 이름별 소비 순서를 분리합니다.
// 반환 source는 소유 scope가 살아 있는 동안 재사용하며 직접 seed를 변경하지 않습니다.

namespace TeamHJD.Game.Contracts
{
    public interface IRandomStreams
    {
        bool IsAvailable { get; }

        /// <summary>Feature 초기화 시 안정적인 이름으로 난수 흐름을 받습니다. 같은 이름은 같은 상태를 공유합니다.</summary>
        /// <param name="streamId">1~128자의 영문 소문자·숫자·점·하이픈·밑줄·슬래시. 예: weather, combat/cannon.</param>
        /// <remarks>메인 스레드에서 사용합니다. Match 종료·권한 상실 후 생성과 소비는 거부됩니다.</remarks>
        IRandomSource GetStream(string streamId);
    }
}
