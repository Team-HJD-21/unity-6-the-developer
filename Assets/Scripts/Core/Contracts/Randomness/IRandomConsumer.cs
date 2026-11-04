// Scene Feature가 Core의 공용 난수를 명시적으로 주입받는 접점입니다.
// 반환 바인딩 해제 시 source 사용을 차단하며 공용 RandomBinding으로 연결할 수 있습니다.

using System;

namespace TeamHJD.Game.Contracts
{
    public interface IRandomConsumer
    {

        /// <summary>권위 Scene 조립부가 Feature 시작 전에 호출합니다. 소비자는 필요한 이름의 흐름을 보관합니다.</summary>
        /// <returns>Core가 Match 종료 또는 조립 실패 시 해제할 바인딩 lease입니다. null은 허용하지 않습니다.</returns>
        /// <remarks>
        /// 공용 RandomBinding.Bind의 반환값을 그대로 반환할 수 있습니다. Core가 해제를 소유합니다.
        /// Client에는 권위 난수를 주입하지 않습니다. 게임 결과는 Host가 결정해 복제합니다.
        /// </remarks>
        IDisposable BindRandom(IRandomStreams random);
    }
}
