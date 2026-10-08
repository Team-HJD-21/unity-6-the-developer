using UnityEngine;
using UnityEngine.Serialization;

/// <summary>활성 총구에서 산탄을 발사하고 실제 발사에 맞춰 효과와 재사용 시간을 갱신합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("bulletPrefab")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private PlayerMuzzleAnchor _muzzleAnchor;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    [FormerlySerializedAs("inputHandler")]
    [SerializeField] private PlayerInputHandler _input;
    [FormerlySerializedAs("playerRb")]
    [SerializeField] private Rigidbody2D _playerBody;

    [Header("Weapons")]
    [FormerlySerializedAs("currentWeapon")]
    [SerializeField] private WeaponType _currentWeapon = WeaponType.DefaultShotgun;
    [FormerlySerializedAs("weapon1Color")]
    [SerializeField] private Color _shotgunColor = new Color(1f, .5f, .1f);
    [FormerlySerializedAs("weapon1Damage")]
    [SerializeField, Min(0f)] private float _shotgunDamage = 13f;
    [FormerlySerializedAs("weapon1FireRate")]
    [SerializeField, Min(.01f)] private float _shotgunInterval = .2f;
    [FormerlySerializedAs("weapon2Color")]
    [SerializeField] private Color _cryoColor = new Color(.2f, .8f, 1f);
    [FormerlySerializedAs("weapon2Damage")]
    [SerializeField, Min(0f)] private float _cryoDamage = 8f;
    [FormerlySerializedAs("weapon2FireRate")]
    [SerializeField, Min(.01f)] private float _cryoInterval = .25f;
    [FormerlySerializedAs("pelletCount")]
    [SerializeField, Range(1, 32)] private int _pelletCount = 5;
    [FormerlySerializedAs("spreadAngle")]
    [SerializeField, Range(0f, 180f)] private float _spreadAngle = 25f;
    private PlayerSkillController _skills;
    private float _nextShotTime;
    private bool _reportedConfigurationError;
    public WeaponType CurrentWeapon => _currentWeapon;

    private void Awake()
    {
        if (_input == null) _input = GetComponent<PlayerInputHandler>();
        if (_playerBody == null) _playerBody = GetComponent<Rigidbody2D>();
        if (_muzzleAnchor == null) _muzzleAnchor = GetComponentInChildren<PlayerMuzzleAnchor>(true);
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        _skills = GetComponent<PlayerSkillController>();
    }

    private void Update()
    {
        if (_input == null || (_skills != null && !_skills.CanUse)) return;
        if (_input.WasWeaponSwitchPressed)
            _currentWeapon = _currentWeapon == WeaponType.DefaultShotgun ? WeaponType.CryoBlaster : WeaponType.DefaultShotgun;
        if (_input.IsAttackPressed) TryFire();
    }

    public bool TryFire()
    {
        if (!isActiveAndEnabled || (_skills != null && !_skills.CanUse) || Time.time < _nextShotTime) return false;
        if (_input == null || _input.transform != transform || _bulletPrefab == null ||
            _bulletPrefab.GetComponent<PlayerBullet>() == null || _muzzleAnchor == null ||
            _weaponRig == null || _weaponRig.PlayerRoot != transform || _muzzleAnchor.WeaponRig != _weaponRig ||
            !_muzzleAnchor.TryGetMuzzle(out var muzzle))
        {
            if (!_reportedConfigurationError)
            {
                Debug.LogWarning("[PlayerAttack] 같은 플레이어의 Weapon Rig, Muzzle Anchor와 PlayerBullet 프리팹을 연결하세요. 총구가 없으면 사격하지 않습니다.", this);
                _reportedConfigurationError = true;
            }
            return false;
        }
        Vector2 direction = _input.HasAimTarget ? _input.MouseWorldPosition - (Vector2)transform.position : _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) return false;
        direction.Normalize();
        Vector2 inheritedVelocity = _playerBody != null ? _playerBody.linearVelocity : Vector2.zero;
        bool shotgun = _currentWeapon == WeaponType.DefaultShotgun;
        int pellets = Mathf.Clamp(_pelletCount, 1, 32);
        float spread = Mathf.Clamp(_spreadAngle, 0f, 180f);
        float step = pellets > 1 ? spread / (pellets - 1) : 0f;
        for (int i = 0; i < pellets; i++)
        {
            float angle = pellets > 1 ? -spread * .5f + step * i : 0f;
            Vector2 shotDirection = Quaternion.Euler(0, 0, angle) * direction;
            var bullet = Instantiate(_bulletPrefab, muzzle.position, Quaternion.identity).GetComponent<PlayerBullet>();
            bullet.InitBullet(_currentWeapon, shotgun ? _shotgunColor : _cryoColor,
                Mathf.Max(0f, shotgun ? _shotgunDamage : _cryoDamage), shotDirection, inheritedVelocity);
        }
        _nextShotTime = Time.time + Mathf.Max(.01f, shotgun ? _shotgunInterval : _cryoInterval);
        _weaponRig.ShowShotFlash();
        return true;
    }
}
