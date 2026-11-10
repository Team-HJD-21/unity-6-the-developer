// 폭발 피해 적용 이후의 애니메이션 진행을 보존합니다. 이미 적용한 범위 피해는 반복하지 않습니다.
/// <summary>
/// 폭발 완료 여부와 제거 대기 상태를 재발동 없이 복원한다.
/// </summary>
public sealed partial class EnemyExplosionAbility
{
    internal bool CheckpointExploded => _hasExploded;
    internal bool CheckpointRemovalPending => _removalDeferred;
    internal void RestoreCheckpointExplosion(bool exploded, bool removalPending)
    {
        _hasExploded = exploded;
        _removalDeferred = removalPending;
        _completionReported = false;
    }
}
