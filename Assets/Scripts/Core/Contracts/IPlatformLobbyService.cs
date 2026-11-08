// 플랫폼 로비 생성·참가·초대 의도를 전달합니다. NGO 서버나 Match는 자동 시작하지 않습니다.
// 로비 ID와 플랫폼 사용자는 게임 연결 ID·Match 참가자 ID와 구분합니다.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface IPlatformLobbyService
    {
        PlatformLobby CurrentLobby { get; }

        event Action<string> JoinRequested;
        Task<PlatformLobby> CreateLobbyAsync(CancellationToken cancellation = default);
        Task<PlatformLobby> JoinLobbyAsync(string lobbyId, CancellationToken cancellation = default);
        void LeaveLobby();
        void OpenInviteOverlay();
    }
}
