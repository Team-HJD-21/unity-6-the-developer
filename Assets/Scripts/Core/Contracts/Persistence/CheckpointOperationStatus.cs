// 예상 가능한 요청 거부·취소와 실제 저장/복원/기술적 실패를 구분합니다.
namespace TeamHJD.Game.Contracts
{
    public enum CheckpointOperationStatus { Saved, Restored, NotFound, Busy, Rejected, Cancelled, Failed, Faulted }
}
