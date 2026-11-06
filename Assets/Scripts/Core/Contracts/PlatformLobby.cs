// 플랫폼 로비의 현재 멤버를 복사한 읽기 자료입니다. 전장 상태는 포함하지 않습니다.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Contracts
{
    public sealed class PlatformLobby
    {
        public string LobbyId { get; }
        public string OwnerId { get; }
        public IReadOnlyList<string> MemberIds { get; }

        public PlatformLobby(string lobbyId, string ownerId, IEnumerable<string> memberIds)
        {
            LobbyId = lobbyId;
            OwnerId = ownerId;
            MemberIds = new ReadOnlyCollection<string>(new List<string>(memberIds));
        }
    }
}
