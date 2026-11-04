// 조립부가 전달한 NGO의 서버 시각을 공용 timestamp 계약에 연결합니다.
// 연결 준비·종료 상태를 시각 0과 구분하고 SDK 시계를 직접 초기화하거나 정지하지 않습니다.
using TeamHJD.Game.Contracts;
using Unity.Netcode;

namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoTimestampSource : ITimestampSource
    {
        private readonly NetworkManager _manager;
        public NgoTimestampSource(NetworkManager manager)
        { _manager = manager != null ? manager : throw new System.ArgumentNullException(nameof(manager)); }
        /// <inheritdoc />
        public bool TryReadSeconds(out double seconds)
        {
            seconds = 0;
            if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress ||
                (!_manager.IsServer && !_manager.IsConnectedClient)) return false;
            seconds = _manager.ServerTime.Time;
            return true;
        }
    }
}
