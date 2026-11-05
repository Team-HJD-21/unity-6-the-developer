// App이 플랫폼 SDK의 콜백과 초기화 상태를 관리하는 접점입니다.
// 게임 시간 정지 중에도 Pump하며 App 종료 때 Dispose합니다.
using System;

namespace TeamHJD.Game.Contracts
{
    public interface IPlatformRuntime : IPlatformService, IDisposable
    {
        string Provider { get; }

        uint ApplicationId { get; }

        bool IsInitialized { get; }

        PlatformUser CurrentUser { get; }

        string LastError { get; }

        void Pump();
    }
}
