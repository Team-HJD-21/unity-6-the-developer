// 체크포인트 복원 시 재화를 현재 값으로 유지할지 캡처 값으로 되돌릴지 선택합니다.
namespace TeamHJD.Game.Domain
{
    public enum CurrencyRestorePolicy { PreserveCurrent, RestoreCheckpoint }
}
