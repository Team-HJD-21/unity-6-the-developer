// Scene Feature가 Core의 공용 난수를 명시적으로 주입받는 접점입니다.
// 바인딩 구현은 반환 lease 해제 시 자신이 보관한 source를 비웁니다.

using System;

namespace TeamHJD.Game.Contracts
{
    public interface IRandomConsumer
    {

        /// <summary>권위 Scene 조립부가 Feature 시작 전에 호출합니다. 소비자는 필요한 이름의 흐름을 보관합니다.</summary>
        /// <returns>Core가 Match 종료 또는 조립 실패 시 해제할 바인딩 lease입니다. null은 허용하지 않습니다.</returns>
        /// <remarks>Client에는 권위 난수를 주입하지 않습니다. 게임 결과는 Host가 결정해 복제합니다.</remarks>
        IDisposable BindRandom(IRandomStreams random);
    }
}
