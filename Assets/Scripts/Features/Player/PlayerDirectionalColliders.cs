using System;
using UnityEngine;

/// <summary>프리팹에 배치한 몸 충돌·탄환 피격 콜라이더를 조준 방향에 맞게 선택합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDirectionalColliders : MonoBehaviour
{
    [Serializable]
    private sealed class DirectionColliders
    {
        public PlayerAimFacing facing = PlayerAimFacing.Down;
        public Collider2D body = null;
        public Collider2D bullet = null;
        public Collider2D solidBody = null;
    }

    [SerializeField] private DirectionColliders[] _directions;
    private int _activeFacing = -1;

    public Bounds GroundBounds
    {
        get
        {
            if (_directions != null)
                foreach (var pair in _directions)
                    if (pair != null && pair.body != null && pair.body.enabled)
                        return pair.body.bounds;
            return new Bounds(transform.position, Vector3.zero);
        }
    }

    public Vector2 GroundPosition => GroundBounds.center;

    public bool IsConfigured
    {
        get
        {
            if (_directions == null || (_directions.Length != 2 && _directions.Length != 8)) return false;
            int seen = 0;
            for (int i = 0; i < _directions.Length; i++)
            {
                var pair = _directions[i];
                if (pair == null || pair.body == null || pair.bullet == null ||
                    pair.body.isTrigger || !pair.bullet.isTrigger) return false;
                if (pair.solidBody != null && (pair.solidBody.isTrigger ||
                    pair.solidBody == pair.body || pair.solidBody == pair.bullet)) return false;
                int facing = (int)pair.facing;
                if (facing < 0 || facing >= 8 || (seen & (1 << facing)) != 0) return false;
                for (int previous = 0; previous < i; previous++)
                    if (_directions[previous].body == pair.body ||
                        _directions[previous].bullet == pair.bullet) return false;
                seen |= 1 << facing;
            }
            return _directions.Length == 8 || seen ==
                ((1 << (int)PlayerAimFacing.Left) | (1 << (int)PlayerAimFacing.Right));
        }
    }

    private void Awake()
    {
        if (!IsConfigured)
        {
            Debug.LogWarning("Player directional colliders need Left/Right pairs or all eight facing pairs.", this);
            enabled = false;
            return;
        }
        SetFacing(_directions.Length == 2 ? PlayerAimFacing.Left : PlayerAimFacing.Down);
    }

    public void SetFacing(PlayerAimFacing facing)
    {
        if (!isActiveAndEnabled || !IsConfigured || (int)facing < 0 || (int)facing >= 8 ||
            _activeFacing == (int)facing) return;
        bool hasFacing = false;
        foreach (var pair in _directions)
            if (pair.facing == facing) { hasFacing = true; break; }
        // A vertical request must not disable both colliders on a horizontal-only actor.
        if (!hasFacing) return;
        _activeFacing = (int)facing;
        foreach (var pair in _directions)
        {
            bool active = pair.facing == facing;
            pair.body.enabled = active;
            pair.bullet.enabled = active;
            if (pair.solidBody != null) pair.solidBody.enabled = active;
        }
    }
}
