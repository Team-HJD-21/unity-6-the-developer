// 모든 Core 메시지 채널의 자원 상한입니다. 게임 난이도/밸런스 설정과 무관합니다.
namespace TeamHJD.Game.Infrastructure
{
    public static class NetworkMessageLimits
    {
        public const int MaximumMessageBytes = 4 * 1024 * 1024;
    }
}
