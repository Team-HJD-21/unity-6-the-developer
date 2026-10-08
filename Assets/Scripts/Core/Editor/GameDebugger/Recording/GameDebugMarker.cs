// Editor에서 관측한 터렛·전선 변화와 사용자의 북마크입니다. 게임 사실 이벤트와 구분합니다.
using System;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugMarker
    {
        public double GameSeconds, RealSeconds;
        public string Message;
        public int TurretId;
    }
}
