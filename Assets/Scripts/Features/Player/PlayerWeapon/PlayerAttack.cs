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
    [SerializeField] private PlayerSpannerWeapon _spannerPrefab;
    private PlayerSpannerWeapon _spanner;

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
    private PlayerSpriteMotion _motion;
    private float _nextShotTime;
    private bool _reportedConfigurationError;
    public WeaponType CurrentWeapon => _currentWeapon;

    private void Awake()
    {
        if (_input == null) _input = GetComponent<PlayerInputHandler>();
        if (_muzzleAnchor == null) _muzzleAnchor = GetComponentInChildren<PlayerMuzzleAnchor>(true);
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        _skills = GetComponent<PlayerSkillController>();
        _motion = GetComponent<PlayerSpriteMotion>();
        if (_spannerPrefab != null)
        {
            _spanner = Instantiate(_spannerPrefab, transform);
            _spanner.Bind(transform, _input);
        }
        RefreshWeaponVisual();
    }

    private void Update()
    {
        if (_input == null || (_skills != null && !_skills.CanUse)) return;
        if (_input.WasWeaponSwitchPressed)
            SelectNextWeapon();
        if (_input.IsAttackPressed) TryFire();
    }

    public bool TryFire()
    {
        if (!isActiveAndEnabled || (_skills != null && !_skills.CanUse) || Time.time < _nextShotTime) return false;
        if (_currentWeapon == WeaponType.Spanner) return TrySwingSpanner();
        if (!TryGetMuzzle(out Transform muzzle) || !TryGetAimDirection(out Vector2 direction)) return false;

        bool isShotgun = _currentWeapon == WeaponType.DefaultShotgun;
        Color color = isShotgun ? _shotgunColor : _cryoColor;
        float damage = Mathf.Max(0f, isShotgun ? _shotgunDamage : _cryoDamage);
        if (!FirePellets(muzzle.position, direction, color, damage)) return false;

        float interval = isShotgun ? _shotgunInterval : _cryoInterval;
        _nextShotTime = Time.time + Mathf.Max(.01f, interval);
        _weaponRig.ShowShotFlash(color);
        if (_motion != null) _motion.PlayRangedShot();
        return true;
    }

    private bool TryGetMuzzle(out Transform muzzle)
    {
        muzzle = null;
        if (_input == null || _input.transform != transform || _bulletPrefab == null ||
            _bulletPrefab.GetComponent<PlayerBullet>() == null || _muzzleAnchor == null ||
            _weaponRig == null || _weaponRig.PlayerRoot != transform || _muzzleAnchor.WeaponRig != _weaponRig ||
            !_muzzleAnchor.TryGetMuzzle(out muzzle))
        {
            if (!_reportedConfigurationError)
            {
                Debug.LogWarning("[PlayerAttack] 같은 플레이어의 Weapon Rig, Muzzle Anchor와 PlayerBullet 프리팹을 연결하세요. 총구가 없으면 사격하지 않습니다.", this);
                _reportedConfigurationError = true;
            }
            return false;
        }
        return true;
    }

    private bool TryGetAimDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (_input == null) return false;
        direction = _input.HasAimTarget ? _input.MouseWorldPosition - (Vector2)transform.position : _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) return false;
        direction.Normalize();
        return true;
    }

    private bool FirePellets(Vector3 spawnPosition, Vector2 direction, Color color, float damage)
    {
        int pelletCount = Mathf.Clamp(_pelletCount, 1, 32);
        float spreadAngle = Mathf.Clamp(_spreadAngle, 0f, 180f);
        float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0f;
        bool fired = false;
        for (int pelletIndex = 0; pelletIndex < pelletCount; pelletIndex++)
        {
            float angle = pelletCount > 1 ? -spreadAngle * .5f + angleStep * pelletIndex : 0f;
            Vector2 pelletDirection = Quaternion.Euler(0f, 0f, angle) * direction;
            var bullet = Instantiate(_bulletPrefab, spawnPosition, Quaternion.identity).GetComponent<PlayerBullet>();
            if (bullet.Initialize(color, damage, pelletDirection, _currentWeapon == WeaponType.CryoBlaster)) fired = true;
            else Destroy(bullet.gameObject);
        }
        return fired;
    }

    public void SelectNextWeapon()
    {
        switch (_currentWeapon)
        {
            case WeaponType.DefaultShotgun: _currentWeapon = WeaponType.CryoBlaster; break;
            case WeaponType.CryoBlaster:
                _currentWeapon = _spanner != null ? WeaponType.Spanner : WeaponType.DefaultShotgun;
                break;
            default: _currentWeapon = WeaponType.DefaultShotgun; break;
        }
        RefreshWeaponVisual();
    }

    private void RefreshWeaponVisual()
    {
        bool melee = _currentWeapon == WeaponType.Spanner && _spanner != null;
        if (_weaponRig != null) _weaponRig.SetRangedWeaponVisible(!melee);
        if (_spanner != null) _spanner.gameObject.SetActive(melee);
    }

    private bool TrySwingSpanner()
    {
        if (_spanner == null || !TryGetAimDirection(out Vector2 direction) || !_spanner.TryAttack(direction)) return false;
        _nextShotTime = Time.time + _spanner.AttackInterval;
        return true;
    }
}
