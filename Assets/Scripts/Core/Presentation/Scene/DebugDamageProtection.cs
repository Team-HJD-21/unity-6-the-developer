// 장시간 통합 검사에서 선택한 객체의 피해만 막는 개발용 표식입니다.
// Game Debugger/검사 코드가 런타임에 붙이며 체크포인트·성장 저장에는 포함하지 않습니다.
// 일반 Release 빌드에서는 컴포넌트가 있어도 피해 보호가 동작하지 않습니다.
// CanReceiveDamage를 호출하는 체력 원본에 적용되며 현재 연결 대상은 Player와 CU입니다.
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent]
    public sealed class DebugDamageProtection : MonoBehaviour
    {
        public static bool IsProtected(Component target)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var protection = target != null ? target.GetComponentInParent<DebugDamageProtection>() : null;
            return protection != null && protection.isActiveAndEnabled;
#else
            return false;
#endif
        }
    }
}
