using UnityEngine;
using UnityEngine.Serialization;

public enum PlayerAimFacing { Down, Right, Up, Left }

public static class PlayerAimUtility
{
    public static PlayerAimFacing GetFacing(Vector2 direction)
    {
        if (!IsValidDirection(direction)) return PlayerAimFacing.Down;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle > -45f && angle <= 45f) return PlayerAimFacing.Right;
        if (angle > 45f && angle <= 135f) return PlayerAimFacing.Up;
        if (angle > -135f && angle <= -45f) return PlayerAimFacing.Down;
        return PlayerAimFacing.Left;
    }

    public static bool IsValidDirection(Vector2 direction) =>
        !float.IsNaN(direction.x) && !float.IsNaN(direction.y) &&
        !float.IsInfinity(direction.x) && !float.IsInfinity(direction.y) && direction.sqrMagnitude > .0001f;
}

/// <summary>플레이어에 총기를 고정하고 몸체 방향, 활성 총기와 총구를 같은 기준으로 선택합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class PlayerWeaponRig : MonoBehaviour
{
    [System.Serializable]
    private sealed class MuzzlePose
    {
        public Vector2 down = Vector2.zero;
        public Vector2 right = Vector2.zero;
        public Vector2 up = Vector2.zero;
        public Vector2 left = Vector2.zero;

        public Vector2 GetPosition(PlayerAimFacing facing)
        {
            switch (facing)
            {
                case PlayerAimFacing.Right: return right;
                case PlayerAimFacing.Up: return up;
                case PlayerAimFacing.Left: return left;
                default: return down;
            }
        }
    }

    [Header("Owner and Mount")]
    [FormerlySerializedAs("Playerpoint"), FormerlySerializedAs("playerpoint")]
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private PlayerInputHandler _input;
    [SerializeField] private Vector3 _mountLocalPosition;
    [SerializeField] private bool _bodyIncludesGunSprite;

    [Header("Directional Guns")]
    [FormerlySerializedAs("GunsideL"), FormerlySerializedAs("gunsideL")]
    [SerializeField] private Transform _leftGun;
    [FormerlySerializedAs("GunsideR"), FormerlySerializedAs("gunsideR")]
    [SerializeField] private Transform _rightGun;
    [FormerlySerializedAs("Gunup"), FormerlySerializedAs("gunup")]
    [SerializeField] private Transform _upGun;
    [FormerlySerializedAs("Gundown"), FormerlySerializedAs("gundown")]
    [SerializeField] private Transform _downGun;

    [Header("Muzzles (children of their gun)")]
    [SerializeField] private Transform _leftMuzzle;
    [SerializeField] private Transform _rightMuzzle;
    [SerializeField] private Transform _upMuzzle;
    [SerializeField] private Transform _downMuzzle;
    [Header("Firing positions (Weapon Rig local coordinates)")]
    [SerializeField] private bool _usePoseMuzzles;
    [SerializeField] private MuzzlePose _firingMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _leftLeanMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _rightLeanMuzzles = new MuzzlePose();
    private PlayerSkillController _skills;
    private Transform _activeGun;
    private Transform _activeMuzzle;
    private PlayerMuzzleFlash _activeFlash;
    private bool _initialized;
    private bool _showGuns = true;

    public void SetRangedWeaponVisible(bool visible)
    {
        _showGuns = visible;
        RefreshPose(_input != null ? _input.AimDirection : Vector2.down);
    }

    public PlayerAimFacing Facing { get; private set; } = PlayerAimFacing.Down;
    public Transform PlayerRoot => _playerRoot;
    public Transform ActiveGun => _activeGun;
    public Transform ActiveMuzzle => _activeMuzzle;

    private void Awake() => Initialize();
    private void OnEnable() { if (_initialized) RefreshPose(Vector2.down); }

    private void Initialize()
    {
        if (_playerRoot == null)
        {
            var input = GetComponentInParent<PlayerInputHandler>();
            if (input != null) _playerRoot = input.transform;
        }
        if (_playerRoot == null || _playerRoot == transform || _playerRoot.IsChildOf(transform))
        {
            Debug.LogWarning("[PlayerWeaponRig] 별도 총기 루트에 유효한 Player Root를 연결하세요.", this);
            enabled = false;
            return;
        }
        if (_input == null || _input.transform != _playerRoot) _input = _playerRoot.GetComponent<PlayerInputHandler>();
        _skills = _playerRoot.GetComponent<PlayerSkillController>();
        if (transform.parent != _playerRoot) transform.SetParent(_playerRoot, false);
        transform.localPosition = _mountLocalPosition;
        transform.localRotation = Quaternion.identity;
        _initialized = true;
        RefreshPose(Vector2.down);
    }

    private void Update() => RefreshPose(_input != null ? _input.AimDirection : Vector2.down);

    public void RefreshPose(Vector2 direction)
    {
        if (!_initialized) return;
        transform.localPosition = _mountLocalPosition;
        Facing = _skills != null && !_skills.CanUse ? PlayerAimFacing.Down : PlayerAimUtility.GetFacing(direction);
        switch (Facing)
        {
            case PlayerAimFacing.Left: Select(_leftGun, _leftMuzzle); break;
            case PlayerAimFacing.Right: Select(_rightGun, _rightMuzzle); break;
            case PlayerAimFacing.Up: Select(_upGun, _upMuzzle); break;
            default: Select(_downGun, _downMuzzle); break;
        }
        UpdateMuzzlePosition();
        SetVisible(_leftGun, _showGuns && _leftGun == _activeGun);
        SetVisible(_rightGun, _showGuns && _rightGun == _activeGun);
        SetVisible(_upGun, _showGuns && _upGun == _activeGun);
        SetVisible(_downGun, _showGuns && _downGun == _activeGun);
        SetGunSpriteVisible(_leftGun);
        SetGunSpriteVisible(_rightGun);
        SetGunSpriteVisible(_upGun);
        SetGunSpriteVisible(_downGun);
    }

    private void UpdateMuzzlePosition()
    {
        if (!_usePoseMuzzles || _activeMuzzle == null) return;
        MuzzlePose pose = _firingMuzzles;
        if (_input != null && _input.MoveInput.sqrMagnitude > .001f)
        {
            if (_input.MoveInput.x < -.01f) pose = _leftLeanMuzzles;
            else if (_input.MoveInput.x > .01f) pose = _rightLeanMuzzles;
        }
        Vector2 position = pose.GetPosition(Facing);
        _activeMuzzle.position = transform.TransformPoint(new Vector3(position.x, position.y, 0));
    }

    private void SetGunSpriteVisible(Transform gun)
    {
        if (gun == null) return;
        SpriteRenderer renderer = gun.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = !_bodyIncludesGunSprite;
    }

    private void Select(Transform gun, Transform muzzle)
    {
        _activeGun = gun != null && gun != transform && gun.IsChildOf(transform) ? gun : null;
        _activeMuzzle = _activeGun != null && muzzle != null && muzzle != gun && muzzle.IsChildOf(gun) ? muzzle : null;
        _activeFlash = _activeGun != null ? _activeGun.GetComponent<PlayerMuzzleFlash>() : null;
    }

    private void SetVisible(Transform gun, bool visible)
    {
        if (gun != null && gun != transform && gun.IsChildOf(transform) && gun.gameObject.activeSelf != visible)
            gun.gameObject.SetActive(visible);
    }

    public bool TryGetMuzzle(out Transform muzzle)
    {
        RefreshPose(_input != null ? _input.AimDirection : Vector2.down);
        muzzle = _activeMuzzle;
        return isActiveAndEnabled && muzzle != null && muzzle.gameObject.activeInHierarchy;
    }

    public void ShowShotFlash() { if (_activeFlash != null) _activeFlash.Show(); }
    public void ShowShotFlash(Color tint) { if (_activeFlash != null) _activeFlash.Show(tint); }
}
