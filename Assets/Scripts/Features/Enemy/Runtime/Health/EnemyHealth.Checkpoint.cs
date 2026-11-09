// 실제 적 체력·피격 허용 상태를 복원합니다. 사망 사건과 피해는 재실행하지 않습니다.
using System.IO;

/// <summary>
/// 피해나 사망 이벤트를 다시 발생시키지 않고 저장된 체력을 복원한다.
/// </summary>
public partial class EnemyHealth
{
    internal void RestoreCheckpoint(int maximum, int health, bool active)
    {
        if (maximum != MaxHealth || health <= 0 || health > maximum) throw new InvalidDataException("Saved enemy health does not match its prefab.");
        currentHealth = health; _isDead = false; _isActive = active;
    }
    internal bool CheckpointDamageActive => _isActive;
}
