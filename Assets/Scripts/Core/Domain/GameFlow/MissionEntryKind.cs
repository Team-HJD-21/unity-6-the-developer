// 새 Mission의 자원 초기화와 같은 실행의 재시도/이어하기를 구분합니다.
namespace TeamHJD.Game.Domain
{
    public enum MissionEntryKind { NewMission, Retry, Resume }
}
