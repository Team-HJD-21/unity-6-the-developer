using UnityEngine;
using UnityEngine.Serialization;

// Keep the original four serialized values. Diagonals are appended.
public enum PlayerAimFacing { Down = 0, Right = 1, Up = 2, Left = 3, DownRight = 4, UpRight = 5, UpLeft = 6, DownLeft = 7 }

public static class PlayerAimUtility
{
    public static bool IsDiagonal(PlayerAimFacing facing) => facing == PlayerAimFacing.DownRight ||
        facing == PlayerAimFacing.UpRight || facing == PlayerAimFacing.UpLeft || facing == PlayerAimFacing.DownLeft;

    public static Vector2 GetFacingDirection(PlayerAimFacing facing)
    {
        switch (facing)
        {
            case PlayerAimFacing.Right: return Vector2.right;
            case PlayerAimFacing.Up: return Vector2.up;
            case PlayerAimFacing.Left: return Vector2.left;
            case PlayerAimFacing.DownRight: return new Vector2(1f, -1f).normalized;
            case PlayerAimFacing.UpRight: return new Vector2(1f, 1f).normalized;
            case PlayerAimFacing.UpLeft: return new Vector2(-1f, 1f).normalized;
            case PlayerAimFacing.DownLeft: return new Vector2(-1f, -1f).normalized;
            default: return Vector2.down;
        }
    }

    // Select the rear 120-degree cone. Perpendicular strafing keeps the normal pose.
    public static bool IsMovingBackward(PlayerAimFacing facing, Vector2 movement) =>
        IsValidDirection(movement) && Vector2.Dot(GetFacingDirection(facing), movement.normalized) < -.5f;

    public static PlayerAimFacing GetEightWayFacing(Vector2 direction)
    {
        if (!IsValidDirection(direction)) return PlayerAimFacing.Down;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int sector = Mathf.FloorToInt((angle + 22.5f + 360f) / 45f) % 8;
        switch (sector)
        {
            case 0: return PlayerAimFacing.Right;
            case 1: return PlayerAimFacing.UpRight;
            case 2: return PlayerAimFacing.Up;
            case 3: return PlayerAimFacing.UpLeft;
            case 4: return PlayerAimFacing.Left;
            case 5: return PlayerAimFacing.DownLeft;
            case 6: return PlayerAimFacing.Down;
            default: return PlayerAimFacing.DownRight;
        }
    }

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
        public Vector2 downRight = Vector2.zero;
        public Vector2 upRight = Vector2.zero;
        public Vector2 upLeft = Vector2.zero;
        public Vector2 downLeft = Vector2.zero;

        public Vector2 GetPosition(PlayerAimFacing facing)
        {
            switch (facing)
            {
                case PlayerAimFacing.DownRight: return downRight;
                case PlayerAimFacing.UpRight: return upRight;
                case PlayerAimFacing.UpLeft: return upLeft;
                case PlayerAimFacing.DownLeft: return downLeft;
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
    [SerializeField] private PlayerWeaponVisual _separatedWeaponVisual;

    [Header("Directional Guns")]
    [FormerlySerializedAs("GunsideL"), FormerlySerializedAs("gunsideL")]
    [SerializeField] private Transform _leftGun;
    [FormerlySerializedAs("GunsideR"), FormerlySerializedAs("gunsideR")]
    [SerializeField] private Transform _rightGun;
    [FormerlySerializedAs("Gunup"), FormerlySerializedAs("gunup")]
    [SerializeField] private Transform _upGun;
    [FormerlySerializedAs("Gundown"), FormerlySerializedAs("gundown")]
    [SerializeField] private Transform _downGun;
    [SerializeField] private Transform _downLeftGun;
    [SerializeField] private Transform _downRightGun;
    [SerializeField] private Transform _upLeftGun;
    [SerializeField] private Transform _upRightGun;

    [Header("Muzzles (children of their gun)")]
    [SerializeField] private Transform _leftMuzzle;
    [SerializeField] private Transform _rightMuzzle;
    [SerializeField] private Transform _upMuzzle;
    [SerializeField] private Transform _downMuzzle;
    [SerializeField] private Transform _downLeftMuzzle;
    [SerializeField] private Transform _downRightMuzzle;
    [SerializeField] private Transform _upLeftMuzzle;
    [SerializeField] private Transform _upRightMuzzle;
    [Header("Firing positions (Weapon Rig local coordinates)")]
    [SerializeField] private bool _usePoseMuzzles;
    [SerializeField] private MuzzlePose _firingMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _leftLeanMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _rightLeanMuzzles = new MuzzlePose();
    [Header("Diagonal idle / movement positions (Weapon Rig local coordinates)")]
    [SerializeField] private MuzzlePose _idleMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _moveMuzzles = new MuzzlePose();
    [SerializeField] private MuzzlePose _backpedalMuzzles = new MuzzlePose();
    private PlayerSkillController _skills;
    private PlayerControlState _controls;
    private PlayerSpriteMotion _motion;
    private PlayerTurretRepair _repair;
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
        _controls = _playerRoot.GetComponent<PlayerControlState>();
        _motion = _playerRoot.GetComponent<PlayerSpriteMotion>();
        _repair = _playerRoot.GetComponent<PlayerTurretRepair>();
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
        if (_repair != null && _repair.TryGetFacingDirection(out Vector2 repairDirection)) direction = repairDirection;
        transform.localPosition = _mountLocalPosition;
        bool bodyOwnsGunVisual = _bodyIncludesGunSprite || HasSeparatedWeaponVisual;
        bool eightWay = _motion != null && (_showGuns
            ? bodyOwnsGunVisual && _motion.SupportsEightWayRanged
            : _motion.OwnsDisplay && _motion.SupportsEightWayTools);
        // 우주선의 전투 제한이 몸체·무기의 방향까지 고정하지 않도록 이동 가능 여부로 표시를 판단합니다.
        bool canFace = _controls != null ? _controls.CanMove : _skills == null || _skills.CanUse;
        Facing = !canFace || (_skills != null && _skills.IsSelectionOpen) ? PlayerAimFacing.Down :
            (eightWay ? PlayerAimUtility.GetEightWayFacing(direction) : PlayerAimUtility.GetFacing(direction));
        // One continuous gun/flash avoids switching effect objects as the mouse
        // crosses the legacy eight-direction boundaries.
        if (_separatedWeaponVisual != null && _separatedWeaponVisual.UsesContinuousAim)
            Select(_rightGun, _rightMuzzle);
        else switch (Facing)
        {
            case PlayerAimFacing.DownLeft: Select(_downLeftGun, _downLeftMuzzle); break;
            case PlayerAimFacing.DownRight: Select(_downRightGun, _downRightMuzzle); break;
            case PlayerAimFacing.UpLeft: Select(_upLeftGun, _upLeftMuzzle); break;
            case PlayerAimFacing.UpRight: Select(_upRightGun, _upRightMuzzle); break;
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
        SetVisible(_downLeftGun, _showGuns && _downLeftGun == _activeGun);
        SetVisible(_downRightGun, _showGuns && _downRightGun == _activeGun);
        SetVisible(_upLeftGun, _showGuns && _upLeftGun == _activeGun);
        SetVisible(_upRightGun, _showGuns && _upRightGun == _activeGun);
        SetGunSpriteVisible(_leftGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_rightGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_upGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_downGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_downLeftGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_downRightGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_upLeftGun, !bodyOwnsGunVisual);
        SetGunSpriteVisible(_upRightGun, !bodyOwnsGunVisual);
    }

    private void UpdateMuzzlePosition(bool forShot = false)
    {
        if (_activeMuzzle != null && _separatedWeaponVisual != null &&
            _separatedWeaponVisual.TryGetMuzzlePosition(out Vector3 weaponMuzzle, forShot))
        {
            _activeMuzzle.position = weaponMuzzle;
            if (_separatedWeaponVisual.UsesContinuousAim)
                _activeMuzzle.rotation = _separatedWeaponVisual.Renderer.transform.rotation;
            return;
        }
        if (!_usePoseMuzzles || _activeMuzzle == null) return;
        MuzzlePose pose = _firingMuzzles;
        bool moving = _input != null && _input.MoveInput.sqrMagnitude > .001f;
        if (PlayerAimUtility.IsDiagonal(Facing))
        {
            if (moving)
                pose = _motion != null && _motion.SupportsDiagonalBackpedal && PlayerAimUtility.IsMovingBackward(Facing, _input.MoveInput)
                    ? _backpedalMuzzles : _moveMuzzles;
            else if (!forShot && (_motion == null || !_motion.IsFiring)) pose = _idleMuzzles;
        }
        else if (moving)
        {
            if (_input.MoveInput.x < -.01f) pose = _leftLeanMuzzles;
            else if (_input.MoveInput.x > .01f) pose = _rightLeanMuzzles;
        }
        Vector2 position = pose.GetPosition(Facing);
        _activeMuzzle.position = transform.TransformPoint(new Vector3(position.x, position.y, 0));
    }

    private void SetGunSpriteVisible(Transform gun, bool visible)
    {
        if (gun == null) return;
        SpriteRenderer renderer = gun.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = visible;
    }

    private bool HasSeparatedWeaponVisual => _separatedWeaponVisual != null && _separatedWeaponVisual.IsConfigured;

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

    public bool TryGetMuzzle(out Transform muzzle, bool forShot = true)
    {
        RefreshPose(_input != null ? _input.AimDirection : Vector2.down);
        // A shot changes Idle to Fire later in the frame: use its authored firing
        // origin now, rather than spawning from the previous displayed idle pose.
        UpdateMuzzlePosition(forShot);
        muzzle = _activeMuzzle;
        return isActiveAndEnabled && muzzle != null && muzzle.gameObject.activeInHierarchy;
    }

    public bool TryGetContinuousAimDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        return _separatedWeaponVisual != null && _separatedWeaponVisual.TryGetContinuousAimDirection(out direction);
    }

    public void ShowShotFlash()
    {
        if (_separatedWeaponVisual != null && _separatedWeaponVisual.UsesContinuousAim) ShowShotFlash(Color.white);
        else if (_activeFlash != null) _activeFlash.Show();
    }
    public void ShowShotFlash(Color tint)
    {
        if (_separatedWeaponVisual != null && _separatedWeaponVisual.UsesContinuousAim)
        {
            if (TryGetMuzzle(out var muzzle) && _activeFlash != null) _activeFlash.Show(tint, muzzle.rotation);
        }
        else if (_activeFlash != null) _activeFlash.Show(tint);
    }
}
