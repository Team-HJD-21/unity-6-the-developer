using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>총구 앞의 짧은 분사 영역에 Player 출처의 피해·감속과 연속 냉기 표시를 연결합니다.</summary>
[DefaultExecutionOrder(350)]
public sealed class PlayerCryoSpray : MonoBehaviour, IPlayerPresentationRandomBinding
{
    [SerializeField] private GameObject _vfxPrefab;
    [SerializeField] private string _randomStream = "vfx/player-cryo-spray";
    [SerializeField, Min(.1f)] private float _range = 4.5f;
    [SerializeField, Min(0f)] private float _muzzleHalfWidth = .12f;
    [SerializeField, Min(.1f)] private float _endHalfWidth = 1.4f;
    [SerializeField, Min(.01f)] private float _slowDuration = 1.5f;
    [SerializeField] private LayerMask _obstacleLayers = 1 | (1 << 12) | (1 << 13) | (1 << 14) | (1 << 21) | (1 << 25);
    private readonly List<Collider2D> _targets = new(32), _overlaps = new(8);
    private readonly List<RaycastHit2D> _walls = new(8);
    private readonly HashSet<EnemyHealth> _damaged = new();
    private ContactFilter2D _enemies, _obstacles, _blockers;
    private GameObject _effect;
    private ParticleSystem[] _particles;
    private readonly ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[256];
    private IRandomSource _random;
    private IDisposable _randomLease;
    private Vector3 _origin;
    private Vector2 _direction;
    public float Range => _range;
    public float SlowDuration => _slowDuration;
    public float SlowMultiplier
    {
        get
        {
            var armory = GetComponentInParent<PlayerArmory>();
            return armory != null && armory.IsConfigured
                ? armory.Settings.SlowMultiplier(armory.Level(WeaponType.CryoBlaster)) : .8f;
        }
    }
    public bool IsConfigured => _vfxPrefab != null && float.IsFinite(_range) && _range > 0 &&
        float.IsFinite(_muzzleHalfWidth) && _muzzleHalfWidth >= 0 && float.IsFinite(_endHalfWidth) && _endHalfWidth > 0;
    public bool IsEmitting { get; private set; }

    private void Awake()
    {
        _enemies = new ContactFilter2D { useTriggers = true };
        _enemies.SetLayerMask(LayerMask.GetMask("EnemyHurtbox"));
        _obstacles = new ContactFilter2D { useTriggers = false };
        _obstacles.SetLayerMask(_obstacleLayers);
        _blockers = new ContactFilter2D { useTriggers = true };
        _blockers.SetLayerMask(_obstacleLayers.value | LayerMask.GetMask("EnemyHurtbox"));
    }

    public void BindPresentationRandom(IRandomStreams streams)
    {
        _randomLease?.Dispose(); _randomLease = null; _random = null;
        _randomLease = RandomBinding.Bind(streams, _randomStream, ref _random);
    }

    /// <summary>영역 안의 각 EnemyHealth에 한 번씩 피해를 전달합니다. 빗나가도 유효한 분사입니다.</summary>
    public bool ApplyDamage(Vector2 origin, Vector2 direction, int damage)
    {
        if (!isActiveAndEnabled || !IsConfigured || !FieldSimulationTime.CanAdvance(this) ||
            !PlayerAimUtility.IsValidDirection(direction) || !Finite(origin) || damage < 0) return false;
        direction.Normalize(); _damaged.Clear();
        Physics2D.SyncTransforms();
        if (IsOriginBlocked(origin)) return true;
        gameObject.scene.GetPhysicsScene2D().OverlapCircle(origin, Mathf.Sqrt(_range * _range + _endHalfWidth * _endHalfWidth), _enemies, _targets);
        foreach (var hitbox in _targets)
        {
            if (hitbox == null || hitbox.gameObject.scene != gameObject.scene) continue;
            var health = hitbox.GetComponentInParent<EnemyHealth>();
            if (health == null || _damaged.Contains(health) || !IsCurrentMatchTarget(health)) continue;
            float along = Mathf.Clamp(Vector2.Dot((Vector2)hitbox.bounds.center - origin, direction), 0, _range);
            Vector2 point = hitbox.ClosestPoint(origin + direction * along);
            if (!Contains(point, origin, direction) || IsObstructed(origin, point, health)) continue;
            _damaged.Add(health);
        }
        // 앞의 적이 이번 틱에 죽어도 같은 틱의 뒤쪽 적에게 판정이 새지 않게 먼저 후보를 확정합니다.
        float slow = SlowMultiplier;
        foreach (var health in _damaged)
        {
            if (health == null || !health.TryTakeDamage(damage, AttackSourceType.Player)) continue;
            if (health != null && health.CanBeAttackedBy(AttackSourceType.Player) && health.TryGetComponent<IAttackSlowEffectReceiver>(out var receiver))
                receiver.TryApplySlow(slow, _slowDuration, AttackSourceType.Player);
        }
        return true;
    }

    public bool Contains(Vector2 point, Vector2 origin, Vector2 direction)
    {
        if (!Finite(point) || !Finite(origin) || !PlayerAimUtility.IsValidDirection(direction)) return false;
        direction.Normalize(); Vector2 delta = point - origin;
        float forward = Vector2.Dot(delta, direction);
        float side = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        return forward >= 0 && forward <= _range && side <= Mathf.Lerp(_muzzleHalfWidth, _endHalfWidth, forward / _range);
    }

    public void Show(Vector3 origin, Vector2 direction)
    {
        if (!isActiveAndEnabled || !IsConfigured || !FieldSimulationTime.CanAdvance(this) ||
            !PlayerAimUtility.IsValidDirection(direction) || !Finite(origin)) { Stop(true); return; }
        _origin = origin; _direction = direction.normalized;
        if (IsOriginBlocked(origin)) { Stop(true); return; }
        if (_random == null || !_random.IsAvailable) { Stop(true); return; }
        if (_effect == null)
        {
            _effect = Instantiate(_vfxPrefab, origin, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(_effect, gameObject.scene);
            _particles = _effect.GetComponentsInChildren<ParticleSystem>(true);
            // 연속 분사는 현재 총구의 범위를 표시합니다. 플레이어가 냉기의 월드 속도를 추월하지 않게 합니다.
            foreach (var ps in _particles)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = _effect.transform;
            }
            var source = GetComponent<SpriteRenderer>();
            foreach (var renderer in _effect.GetComponentsInChildren<ParticleSystemRenderer>())
                if (source != null) { renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + 2; }
        }
        _effect.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg));
        if (IsEmitting) return;
        foreach (var ps in _particles)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)_random.NextInt(1, int.MaxValue);
            ps.Play(false); ps.Pause(false);
        }
        IsEmitting = true;
    }

    public void Stop(bool clear = false)
    {
        if (_particles != null && (IsEmitting || clear))
            foreach (var ps in _particles) if (ps != null)
                ps.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        IsEmitting = false;
    }

    private void LateUpdate()
    {
        if (_effect == null) return;
        if (!FieldSimulationTime.CanAdvance(this) || (_random != null && !_random.IsAvailable)) { Stop(true); return; }
        AdvanceParticles(FieldSimulationTime.DeltaTime(this));
    }

    private void AdvanceParticles(float elapsed)
    {
        foreach (var ps in _particles)
        {
            ps.Simulate(elapsed, false, false, false);
            int count = ps.GetParticles(_particleBuffer);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = _effect.transform.TransformPoint(_particleBuffer[i].position);
                if (!Contains(point, _origin, _direction) || IsObstructed(_origin, point))
                    _particleBuffer[i].remainingLifetime = 0;
            }
            ps.SetParticles(_particleBuffer, count);
        }
    }

    private bool IsOriginBlocked(Vector2 origin)
    {
        gameObject.scene.GetPhysicsScene2D().OverlapPoint(origin, _obstacles, _overlaps);
        foreach (var hit in _overlaps) if (PlayerBullet.IsWorldObstacle(hit, gameObject)) return true;
        return false;
    }
    private bool IsObstructed(Vector2 origin, Vector2 point, EnemyHealth target = null)
    {
        Vector2 delta = point - origin;
        if (delta.sqrMagnitude <= .000001f) return false;
        var physics = gameObject.scene.GetPhysicsScene2D();
        // 총구가 이미 몸체 안에 있어도 반대쪽 면까지 새지 않도록 시작점도 확인합니다.
        physics.OverlapPoint(origin, _blockers, _overlaps);
        foreach (var hit in _overlaps) if (BlocksSpray(hit, target)) return true;
        physics.Raycast(origin, delta.normalized, delta.magnitude, _blockers, _walls);
        foreach (var hit in _walls) if (BlocksSpray(hit.collider, target)) return true;
        return false;
    }

    private bool BlocksSpray(Collider2D hitbox, EnemyHealth target)
    {
        if (PlayerBullet.IsWorldObstacle(hitbox, gameObject)) return true;
        if (hitbox == null || hitbox.gameObject.layer != LayerMask.NameToLayer("EnemyHurtbox")) return false;
        var health = hitbox.GetComponentInParent<EnemyHealth>();
        return health != null && health != target && IsCurrentMatchTarget(health);
    }
    private bool IsCurrentMatchTarget(EnemyHealth health)
    {
        if (!health.CanBeAttackedBy(AttackSourceType.Player) || !FieldSimulationTime.CanAdvance(health)) return false;
        var playerTime = GetComponentInParent<PlayerMatchPresence>()?.TimeSource ?? GetComponentInParent<FieldSimulationActor>()?.TimeSource;
        var enemyTime = health.GetComponentInParent<FieldSimulationActor>()?.TimeSource;
        if (playerTime == null || enemyTime == null) return playerTime == null && enemyTime == null;
        if (!playerTime.IsAvailable || !enemyTime.IsAvailable) return false;
        // Player는 공용 Clock, 기존 필드 적은 FieldClock을 사용하므로 객체 참조는 서로 다릅니다.
        var player = playerTime.Snapshot;
        var enemy = enemyTime.Snapshot;
        return player.MatchId == enemy.MatchId && player.Epoch == enemy.Epoch;
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
    private void OnDisable() => Stop(true);
    private void OnDestroy()
    {
        Stop(true);
        if (_effect != null)
        {
            if (Application.isPlaying) Destroy(_effect); else DestroyImmediate(_effect);
        }
        _randomLease?.Dispose(); _randomLease = null; _random = null;
    }
}
