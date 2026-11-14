// 현재 무기의 입력·발사 대기와 실제 소유 탄환을 관리합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>활성 총구에서 무기별 탄환을 발사하고 실제 발사에 맞춰 효과와 재사용 시간을 갱신합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerWeaponSelection))]
[DefaultExecutionOrder(300)]
public partial class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponSelection _selection;
    [SerializeField] private PlayerMuzzleAnchor _muzzleAnchor;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    [SerializeField] private PlayerCryoSpray _cryoSpray;
    [FormerlySerializedAs("inputHandler")]
    [SerializeField] private PlayerInputHandler _input;

    [Header("Weapons")]
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
    [SerializeField, Range(1, 32)] private int _defaultGunPelletCount = 1;
    [SerializeField, Range(0f, 180f)] private float _defaultGunSpreadAngle = 25f;
    [FormerlySerializedAs("weapon2Color")]
    [SerializeField] private Color _cryoColor = new Color(.2f, .8f, 1f);
    [FormerlySerializedAs("weapon2Damage")]
    [SerializeField, Min(0f)] private float _cryoDamage = 8f;
    [FormerlySerializedAs("weapon2FireRate")]
    [SerializeField, Min(.01f)] private float _cryoInterval = .1f;
    [Header("Sniper")]
    [SerializeField] private Color _sniperColor = new Color(.85f, .92f, 1f);
    [SerializeField, Min(0f)] private float _sniperDamage = 45f;
    [SerializeField, Min(.01f)] private float _sniperInterval = .85f;
    private PlayerSkillController _skills;
    private PlayerSpriteMotion _motion;
    private PlayerTurretRepair _repair;
    private PlayerWeaponAmmo _ammo;
    private PlayerBaseStats _baseStats;
    private double _nextShotTime;
    private double GameSeconds => FieldSimulationTime.Seconds(this);
    private PlayerMatchPresence Presence => GetComponent<PlayerMatchPresence>();
    private bool _reportedConfigurationError;
    /// <summary>탄환 발사 또는 냉기 피해 틱에 성공하면 발생합니다. 막힌 입력에는 발생하지 않습니다.</summary>
    public event System.Action<WeaponType> ShotFired;
    /// <summary>실제 발사 또는 근접 휘두르기에 성공했을 때 발생합니다. 비전투 Hover 표시의 기준입니다.</summary>
    public event System.Action AttackPerformed;
    private bool CanAttack => isActiveAndEnabled && _selection != null && _selection.transform == transform &&
        _selection.CanSelectWeapon && (_skills == null || !_skills.IsPlacingSkill) &&
        (_repair == null || !_repair.IsRepairing);
    private GameObject CurrentBulletPrefab => _selection != null ? _selection.CurrentBulletPrefab : null;

    private void Awake()
    {
        if (_input == null) _input = GetComponent<PlayerInputHandler>();
        if (_selection == null) _selection = GetComponent<PlayerWeaponSelection>();
        if (_muzzleAnchor == null) _muzzleAnchor = GetComponentInChildren<PlayerMuzzleAnchor>(true);
        if (_weaponRig == null) _weaponRig = GetComponentInChildren<PlayerWeaponRig>(true);
        if (_cryoSpray == null) _cryoSpray = GetComponent<PlayerCryoSpray>();
        _skills = GetComponent<PlayerSkillController>();
        _motion = GetComponent<PlayerSpriteMotion>();
        _repair = GetComponent<PlayerTurretRepair>();
        _ammo = GetComponent<PlayerWeaponAmmo>();
        _baseStats = GetComponent<PlayerBaseStats>();
    }

    // 모든 UI Update와 설치 LateUpdate가 클릭을 처리한 뒤에만 공격합니다.
    private void LateUpdate()
    {
        bool held = _input != null && _input.ResolvePrimaryAttack(CanAttack);
        if (held) TryFire();
        if (_cryoSpray == null) return;
        if (held && CanAttack && FieldSimulationTime.CanAdvance(this) && _selection.CurrentWeapon == WeaponType.CryoBlaster &&
            (_ammo == null || _ammo.CanFire(WeaponType.CryoBlaster)) && TryGetMuzzle(out var muzzle) && TryGetAimDirection(out var direction))
            _cryoSpray.Show(muzzle.position, direction);
        else _cryoSpray.Stop(!CanAttack || !FieldSimulationTime.CanAdvance(this));
    }

    private void OnDisable()
    {
        if (_input != null) _input.CancelAttackGesture();
        if (_cryoSpray != null) _cryoSpray.Stop(true);
    }

    public bool TryFire()
    {
        if (!FieldSimulationTime.CanAdvance(this) || !CanAttack || GameSeconds < _nextShotTime) return false;
        // 재장전은 실제 발사만 막습니다. LMB 유지 입력은 보존해 완료 뒤 사격을 이어갑니다.
        if (_ammo != null && !_ammo.CanFire(_selection.CurrentWeapon)) return false;
        if (_selection.CurrentWeapon == WeaponType.Spanner) return TrySwingSpanner();
        if (!TryGetMuzzle(out Transform muzzle) || !TryGetAimDirection(out Vector2 direction)) return false;

        var shot = GetShotSettings();
        bool fired;
        if (shot.cryogenic)
        {
            if (_cryoSpray == null || !_cryoSpray.IsConfigured) return false;
            float damage = _baseStats != null ? _baseStats.ResolveWeaponDamage(shot.damage) : Mathf.Max(0, shot.damage);
            fired = _cryoSpray.ApplyDamage(muzzle.position, direction, Mathf.RoundToInt(damage));
        }
        else fired = FirePellets(muzzle.position, direction, shot.color, Mathf.Max(0f, shot.damage),
            false, shot.pelletCount, shot.spreadAngle);
        if (!fired) return false;

        // 한 번의 산탄 발사도 탄창에서는 한 발입니다. 생성 실패는 소비하지 않습니다.
        if (_ammo != null) _ammo.TryConsumeRound(_selection.CurrentWeapon);

        _nextShotTime = GameSeconds + Mathf.Max(.01f, shot.interval);
        if (!shot.cryogenic) _weaponRig.ShowShotFlash(shot.color);
        if (_motion != null) _motion.PlayRangedShot();
        ShotFired?.Invoke(_selection.CurrentWeapon);
        AttackPerformed?.Invoke();
        TeamHJD.Game.Presentation.GameAudioManager.Play(TeamHJD.Game.Presentation.GameSound.PlayerShot, this);
        return true;
    }

    private (Color color, float damage, float interval, int pelletCount, float spreadAngle, bool cryogenic)
        GetShotSettings() => _selection.CurrentWeapon switch
        {
            WeaponType.CryoBlaster => (_cryoColor, _cryoDamage, _cryoInterval, 0, 0f, true),
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
        if (_weaponRig != null && _weaponRig.TryGetContinuousAimDirection(out direction)) return true;
        direction = _input.HasAimTarget ? _input.MouseWorldPosition - (Vector2)transform.position : _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = _input.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) return false;
        direction.Normalize();
        return true;
    }

    private bool FireProjectile(Vector3 spawnPosition, Vector2 direction, Color color, float damage, bool cryogenic)
    {
        if (_baseStats != null) damage = _baseStats.ResolveWeaponDamage(damage);
        var bullet = Instantiate(CurrentBulletPrefab, spawnPosition, Quaternion.identity).GetComponent<PlayerBullet>();
        FieldSimulationTime.BindSpawned(bullet.gameObject, this);
        bullet.SourceWeapon = _selection.CurrentWeapon;
        if (bullet.Initialize(color, damage, direction, cryogenic)) { TrackProjectile(bullet); return true; }
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

    private bool TrySwingSpanner()
    {
        var spanner = _selection.Spanner;
        if (spanner == null || !TryGetAimDirection(out Vector2 direction) || !spanner.TryAttack(direction)) return false;
        _nextShotTime = GameSeconds + spanner.AttackInterval;
        AttackPerformed?.Invoke();
        return true;
    }
}
