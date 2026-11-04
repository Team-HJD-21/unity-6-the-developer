using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>개발 중 G키로 대시 모션만 확인합니다. 실제 이동 입력과 물리는 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSpriteMotion))]
[DefaultExecutionOrder(50)]
[AddComponentMenu("Player/Debug/Dash Animation Preview")]
public sealed class PlayerDashAnimationPreview : MonoBehaviour
{
    [SerializeField] private PlayerSpriteMotion _motion;

    private void Reset() => _motion = GetComponent<PlayerSpriteMotion>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Awake()
    {
        if (_motion == null) _motion = GetComponent<PlayerSpriteMotion>();
    }

    private void Update()
    {
        if (Keyboard.current?.gKey.wasPressedThisFrame == true && _motion != null)
            _motion.TryPlayDashAnimation();
    }
#endif
}
