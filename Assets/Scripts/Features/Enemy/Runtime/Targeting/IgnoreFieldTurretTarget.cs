using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 적을 필드 터렛의 타깃 후보에서 제외하도록 표시한다.
/// 적 루트에 부착하며, 소비자는 후보 Collider의 부모에서 이 타입을 조회한다.
/// 표시의 존재 자체가 규칙이며, 물리 검색이나 피해 판정을 직접 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Enemy/Targeting/Ignore Field Turret Target")]
public sealed class IgnoreFieldTurretTarget : MonoBehaviour
{
}
