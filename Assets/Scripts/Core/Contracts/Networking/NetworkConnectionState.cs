// 네트워크 연결 수명입니다. 게임의 Preparing/Running Match 단계와 별개입니다.
namespace TeamHJD.Game.Contracts
{
    public enum NetworkConnectionState { Disconnected, StartingHost, StartingClient, Hosting, ConnectedClient, Stopping, Faulted, Disposed }
}
