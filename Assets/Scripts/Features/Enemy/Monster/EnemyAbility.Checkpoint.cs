// 실제 능력 재사용 대기를 Match의 남은 시간으로 복원합니다.
// 폭발 여부는 파생 Feature에서 적용하며 발동/피해 함수를 다시 호출하지 않습니다.
using System;
using System.IO;
using TeamHJD.Game.Presentation.Scene;

public abstract partial class EnemyAbility
{
    internal double CheckpointCooldown => Math.Max(0, _nextUseTime - FieldSimulationTime.Seconds(this));
    internal void RestoreCheckpointCooldown(double remaining)
    {
        if (!double.IsFinite(remaining) || remaining < 0 || remaining > cooldown) throw new InvalidDataException("Invalid enemy ability cooldown.");
        _nextUseTime = FieldSimulationTime.Seconds(this) + remaining;
    }
}
