using UnityEngine;

/// <summary>수동으로 배치한 방향별 몸 충돌·탄환 피격 콜라이더를 선택합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDirectionalColliders : MonoBehaviour
{
    [Header("Body Collision")]
    [SerializeField] private Collider2D _bodyUpDown;
    [SerializeField] private Collider2D _bodyLeft;
    [SerializeField] private Collider2D _bodyRight;

    [Header("Bullet Hitboxes")]
    [SerializeField] private Collider2D _bulletUpDown;
    [SerializeField] private Collider2D _bulletLeft;
    [SerializeField] private Collider2D _bulletRight;

    private int _activeGroup = -1;
    public bool IsConfigured => _bodyUpDown != null && _bodyLeft != null && _bodyRight != null &&
        _bulletUpDown != null && _bulletLeft != null && _bulletRight != null;

    private void Awake()
    {
        if (!IsConfigured)
        {
            Debug.LogWarning("Player directional colliders need all six collider references.", this);
            enabled = false;
            return;
        }
        SetFacing(PlayerAimFacing.Down);
    }

    public void SetFacing(PlayerAimFacing facing)
    {
        if (!isActiveAndEnabled || !IsConfigured) return;
        int group = facing == PlayerAimFacing.Left ? 1 : facing == PlayerAimFacing.Right ? 2 : 0;
        if (group == _activeGroup) return;
        _activeGroup = group;

        _bodyUpDown.enabled = _bulletUpDown.enabled = group == 0;
        _bodyLeft.enabled = _bulletLeft.enabled = group == 1;
        _bodyRight.enabled = _bulletRight.enabled = group == 2;
    }
}
