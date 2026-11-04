// 실제 Feature 원본이 제공하는 캡처·검증·복원 접점입니다. 저장 시점을 결정하지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointParticipant
    {
        string Key { get; }
        int Version { get; }
        CheckpointSectionKind Kind { get; }
        int RestoreOrder { get; }
        /// <summary>정지된 같은 프레임에 실제 원본을 캡처합니다. 게임 상태를 변경하거나 예약을 등록하지 않습니다.</summary>
        CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec);
        /// <summary>목표 schema/정의/값을 확인하고 현재 상태 백업과 복원 단계를 만듭니다. 아직 적용하지 않습니다.</summary>
        ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec);
    }
}
