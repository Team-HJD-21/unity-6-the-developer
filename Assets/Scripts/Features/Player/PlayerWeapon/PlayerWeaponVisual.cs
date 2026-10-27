using UnityEngine;
using UnityEngine.Rendering;

/// <summary>몸체 프레임에 무기를 맞추고, 외형에 지정된 총구 좌표를 Weapon Rig에 제공합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class PlayerWeaponVisual : MonoBehaviour
{
    [Header("Owner and authored renderer")]
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private SpriteRenderer _weapon;
    [SerializeField] private SortingGroup _sortingGroup;
    [SerializeField] private PlayerAttack _attack;
    [SerializeField] private PlayerSpriteMotion _motion;
    [Header("Replace appearances here to change the equipped weapon art")]
    [SerializeField] private PlayerWeaponAppearance _defaultGun;
    [SerializeField] private PlayerWeaponAppearance _cryoBlaster;
    [SerializeField] private PlayerWeaponAppearance _spanner;
    [SerializeField] private PlayerWeaponAppearance _sniper;
    private Sprite _lastMissingFrame;

    public bool IsConfigured => _body != null && _weapon != null && _body != _weapon &&
        _body.transform == transform && _weapon.transform.parent == transform &&
        _sortingGroup != null && _sortingGroup.transform == transform &&
        _attack != null && _attack.transform == transform && _motion != null && _motion.transform == transform &&
        _defaultGun != null && _defaultGun.IsConfigured &&
        _cryoBlaster != null && _cryoBlaster.IsConfigured && _spanner != null && _spanner.IsConfigured &&
        _sniper != null && _sniper.IsConfigured;
    public SpriteRenderer Renderer => _weapon;

    private PlayerWeaponAppearance CurrentAppearance
    {
        get
        {
            switch (_attack.CurrentWeapon)
            {
                case WeaponType.CryoBlaster: return _cryoBlaster;
                case WeaponType.Spanner: return _spanner;
                case WeaponType.Sniper: return _sniper;
                default: return _defaultGun;
            }
        }
    }

    private void Awake()
    {
        if (!IsConfigured)
            Debug.LogWarning("[PlayerWeaponVisual] Connect the body, weapon renderer, sorting group and all weapon appearances.", this);
        Hide();
    }

    // Animator evaluates the body before LateUpdate, including turns and impact pauses.
    private void LateUpdate() => Refresh();

    public bool TryGetMuzzlePosition(out Vector3 position, bool forShot = false)
    {
        position = default;
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled ||
            !CurrentAppearance.TryGetFrame(_body.sprite, out var frame) || !frame.OverrideMuzzle) return false;
        // The first shot starts Fire later in the same frame. Use its authored
        // origin now, rather than the lowered gun still displayed by Idle/Move.
        Vector2 local = forShot ? frame.ShotMuzzleLocalPosition : frame.MuzzleLocalPosition;
        if (_body.flipX) local.x = -local.x;
        if (_body.flipY) local.y = -local.y;
        position = transform.TransformPoint(new Vector3(local.x, local.y));
        return true;
    }

    public void Refresh()
    {
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled)
        {
            Hide();
            return;
        }

        if (!CurrentAppearance.TryGetFrame(_body.sprite, out PlayerWeaponAppearance.Frame frame))
        {
            Hide();
            if (_body.sprite != null && _lastMissingFrame != _body.sprite)
            {
                _lastMissingFrame = _body.sprite;
                Debug.LogWarning($"[PlayerWeaponVisual] Missing weapon frame for {_body.sprite.name}.", this);
            }
            return;
        }

        _lastMissingFrame = null;
        _weapon.sprite = frame.Weapon;
        Vector2 position = frame.LocalPosition;
        if (_body.flipX) position.x = -position.x;
        if (_body.flipY) position.y = -position.y;
        _weapon.transform.localPosition = position;
        float angle = _body.flipX != _body.flipY ? -frame.LocalAngle : frame.LocalAngle;
        _weapon.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        _weapon.flipX = _body.flipX;
        _weapon.flipY = _body.flipY;
        _weapon.color = _body.color;
        _weapon.sortingLayerID = _body.sortingLayerID;
        _weapon.sortingOrder = _body.sortingOrder + frame.SortingOffset;
        // Keep the character together against other actors. The frame's order
        // offset arranges the weapon inside this character, not above every enemy.
        _sortingGroup.sortingLayerID = _body.sortingLayerID;
        _sortingGroup.sortingOrder = _body.sortingOrder;
        _weapon.enabled = true;
    }

    private void Hide()
    {
        if (_weapon != null) _weapon.enabled = false;
    }

    private void OnDisable() => Hide();
}
