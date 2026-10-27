using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>활성 총구에서 무기별 탄환을 발사하고 실제 발사에 맞춰 효과와 재사용 시간을 갱신합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
[DefaultExecutionOrder(300)]
public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("bulletPrefab")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private GameObject _sniperBulletPrefab;
    [SerializeField] private PlayerMuzzleAnchor _muzzleAnchor;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    [FormerlySerializedAs("inputHandler")]
    [SerializeField] private PlayerInputHandler _input;
    [SerializeField] private PlayerSpannerWeapon _spannerPrefab;
    private PlayerSpannerWeapon _spanner;

    [Header("Weapons")]
    [FormerlySerializedAs("currentWeapon")]
    [SerializeField] private WeaponType _currentWeapon = WeaponType.DefaultGun;
    [FormerlySerializedAs("weapon1Color")]
    [FormerlySerializedAs("_shotgunColor")]
    [SerializeField] private Color _defaultGunColor = new Color(1f, .5f, .1f);
    [FormerlySerializedAs("weapon1Damage")]
    [FormerlySerializedAs("_shotgunDamage")]
    [SerializeField, Min(0f)] private float _defaultGunDamage = 13f;
    [FormerlySerializedAs("weapon1FireRate")]
    [FormerlySerializedAs("_shotgunInterval")]
    [SerializeField, Min(.01f)] private float _defaultGunInterval = .2f;
    [Header("Default Gun Spread")]
    [SerializeField, Range(1, 32)] private int _defaultGunPelletCount = 5;
    [SerializeField, Range(0f, 180f)] private float _defaultGunSpreadAngle = 25f;
    [FormerlySerializedAs("weapon2Color")]
    [SerializeField] private Color _cryoColor = new Color(.2f, .8f, 1f);
    [FormerlySerializedAs("weapon2Damage")]
    [SerializeField, Min(0f)] private float _cryoDamage = 8f;
    [FormerlySerializedAs("weapon2FireRate")]
    [SerializeField, Min(.01f)] private float _cryoInterval = .25f;
    [Header("Cryo Spread")]
    [FormerlySerializedAs("pelletCount")]
    [FormerlySerializedAs("_pelletCount")]
    [SerializeField, Range(1, 32)] private int _cryoPelletCount = 5;
    [FormerlySerializedAs("spreadAngle")]
    [FormerlySerializedAs("_spreadAngle")]
    [SerializeField, Range(0f, 180f)] private float _cryoSpreadAngle = 25f;
    [Header("Sniper")]
    [SerializeField] private Color _sniperColor = new Color(.85f, .92f, 1f);
    [SerializeField, Min(0f)] private float _sniperDamage = 45f;
    [SerializeField, Min(.01f)] private float _sniperInterval = .85f;
    private PlayerSkillController _skills;
    private PlayerSpriteMotion _motion;
    private PlayerControlState _control;
    private float _nextShotTime;
    private bool _reportedConfigurationError;
    public WeaponType CurrentWeapon => _currentWeapon;
    public event Action<WeaponType> WeaponChanged;
    public bool CanSelectWeapon => isActiveAndEnabled && Time.timeScale > 0 &&
        (_control == null || _control.CanAct) && (_skills == null || _skills.CanUse);
    private bool CanAttack => CanSelectWeapon && (_skills == null || !_skills.IsPlacingSkill);
    private GameObject CurrentBulletPrefab => _currentWeapon == WeaponType.Sniper ? _sniperBulletPrefab : _bulletPrefab;

    private void Awake()
    {
        if (_input == null) _input = GetComponent<PlayerInputHandler>();
        if (_muzzleAnchor == null) _muzzleAnchor = GetComponentInChildren<PlayerMuzzleAnchor>(true);
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        _skills = GetComponent<PlayerSkillController>();
        _motion = GetComponent<PlayerSpriteMotion>();
        _control = GetComponent<PlayerControlState>();
        if (_spannerPrefab != null)
        {
            _spanner = Instantiate(_spannerPrefab, transform);
            _spanner.Bind(transform, _input);
        }
        RefreshWeaponVisual();
    }

    private void Update()
    {
        if (_input == null || !CanSelectWeapon) return;
        if (_input.RequestedWeaponSlot > 0) TrySelectWeaponSlot(_input.RequestedWeaponSlot);
        else if (_input.WeaponScrollStep != 0) SelectAdjacentWeapon(_input.WeaponScrollStep);
        else if (_input.WasWeaponSwitchPressed) SelectNextWeapon();
    }

    // 모든 UI Update와 설치 LateUpdate가 클릭을 처리한 뒤에만 공격합니다.
    private void LateUpdate()
    {
        if (_input == null) return;
        if (_input.ResolvePrimaryAttack(CanAttack)) TryFire();
    }

    private void OnDisable()
    {
        if (_input != null) _input.CancelAttackGesture();
    }

    public bool TryFire()
    {
        if (!CanAttack || Time.time < _nextShotTime) return false;
        if (_currentWeapon == WeaponType.Spanner) return TrySwingSpanner();
        if (!TryGetMuzzle(out Transform muzzle) || !TryGetAimDirection(out Vector2 direction)) return false;

        var shot = GetShotSettings();
        bool fired = FirePellets(muzzle.position, direction, shot.color, Mathf.Max(0f, shot.damage),
            shot.cryogenic, shot.pelletCount, shot.spreadAngle);
        if (!fired) return false;

        _nextShotTime = Time.time + Mathf.Max(.01f, shot.interval);
        _weaponRig.ShowShotFlash(shot.color);
        if (_motion != null) _motion.PlayRangedShot();
        return true;
    }

    private (Color color, float damage, float interval, int pelletCount, float spreadAngle, bool cryogenic)
        GetShotSettings() => _currentWeapon switch
        {
            WeaponType.CryoBlaster => (_cryoColor, _cryoDamage, _cryoInterval, _cryoPelletCount, _cryoSpreadAngle, true),
            WeaponType.Sniper => (_sniperColor, _sniperDamage, _sniperInterval, 1, 0f, false),
            _ => (_defaultGunColor, _defaultGunDamage, _defaultGunInterval, _defaultGunPelletCount, _defaultGunSpreadAngle, false)
        };

    private bool TryGetMuzzle(out Transform muzzle)
    {
        muzzle = null;
        if (_input == null || _input.transform != transform || CurrentBulletPrefab == null ||
            CurrentBulletPrefab.GetComponent<PlayerBullet>() == null || _muzzleAnchor == null ||
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

    private bool FireProjectile(Vector3 spawnPosition, Vector2 direction, Color color, float damage, bool cryogenic)
    {
        var bullet = Instantiate(CurrentBulletPrefab, spawnPosition, Quaternion.identity).GetComponent<PlayerBullet>();
        if (bullet.Initialize(color, damage, direction, cryogenic)) return true;
        Destroy(bullet.gameObject);
        return false;
    }

    private bool FirePellets(Vector3 spawnPosition, Vector2 direction, Color color, float damage,
        bool cryogenic, int pelletCount, float spreadAngle)
    {
        pelletCount = Mathf.Clamp(pelletCount, 1, 32);
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 180f);
        float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0f;
        bool fired = false;
        for (int pelletIndex = 0; pelletIndex < pelletCount; pelletIndex++)
        {
            float angle = pelletCount > 1 ? -spreadAngle * .5f + angleStep * pelletIndex : 0f;
            Vector2 pelletDirection = Quaternion.Euler(0f, 0f, angle) * direction;
            if (FireProjectile(spawnPosition, pelletDirection, color, damage, cryogenic)) fired = true;
        }
        return fired;
    }

    public bool IsWeaponAvailable(WeaponType weapon) => weapon switch
    {
        WeaponType.Spanner => _spanner != null,
        WeaponType.DefaultGun or WeaponType.CryoBlaster => _bulletPrefab != null,
        WeaponType.Sniper => _sniperBulletPrefab != null,
        _ => false
    };

    public bool TrySelectWeaponSlot(int slot) =>
        PlayerWeaponSlots.TryGetWeapon(slot, out var weapon) && TrySelectWeapon(weapon);

    public bool TrySelectWeapon(WeaponType weapon)
    {
        if (!CanSelectWeapon || !IsWeaponAvailable(weapon)) return false;
        if (_currentWeapon == weapon) return true;
        _currentWeapon = weapon;
        RefreshWeaponVisual();
        WeaponChanged?.Invoke(weapon);
        return true;
    }

    public void SelectNextWeapon() => SelectAdjacentWeapon(1);

    public bool SelectAdjacentWeapon(int direction)
    {
        if (!CanSelectWeapon || direction == 0) return false;
        int slot = PlayerWeaponSlots.GetSlot(_currentWeapon);
        int step = direction > 0 ? 1 : -1;
        if (slot == 0) slot = step > 0 ? 0 : 1;
        for (int attempt = 0; attempt < PlayerWeaponSlots.Count; attempt++)
        {
            slot = (slot - 1 + step + PlayerWeaponSlots.Count) % PlayerWeaponSlots.Count + 1;
            if (PlayerWeaponSlots.TryGetWeapon(slot, out var candidate) && IsWeaponAvailable(candidate))
                return TrySelectWeapon(candidate);
        }
        return false;
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
