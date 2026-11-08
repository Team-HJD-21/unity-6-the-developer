// 터렛 공통 저장부와 종류별 무기 원본의 경계입니다.
// Controller가 공통 actor를 먼저 복원하고 전체 Feature 생성 뒤 타깃 참조를 연결합니다.
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.FieldTurrets
{
    public interface IFieldTurretCombatCheckpoint
    {
        CheckpointSection CaptureCombat(ICheckpointPayloadCodec codec);
        void ValidateCombat(CheckpointSection section, ICheckpointPayloadCodec codec);
        void ReleaseCombat();
        void RestoreCombat(CheckpointSection section, ICheckpointPayloadCodec codec);
        void ResolveCombatReferences();
    }
}
