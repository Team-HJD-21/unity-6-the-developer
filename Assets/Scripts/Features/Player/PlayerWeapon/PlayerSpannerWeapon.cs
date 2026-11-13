// 렌치의 입력·예정 타격·수리 실행을 공용 시간과 Player 행동 조건에 연결합니다.
using TeamHJD.Game.Presentation.Scene;
using System.Collections.Generic;
using UnityEngine;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine.SceneManagement;

/// <summary>렌치 타격을 Match 시간으로 예약하고 명중 순간 대상을 검증해 제작된 피드백을 재생합니다.</summary>
[DisallowMultipleComponent]
public sealed partial class PlayerSpannerWeapon : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _range = 2f;
    [SerializeField, Range(1f, 180f)] private float _arc = 100f;
    [SerializeField, Min(0)] private int _damage = 20;
    [SerializeField, Min(.05f)] private float _attackInterval = .6f;
    [SerializeField, Min(.01f)] private float _swingSeconds = .22f;
    [Header("Right Hand (player local coordinates)")]
    [SerializeField] private Vector2 _downHand = new Vector2(-.6f, -.45f);
    [SerializeField] private Vector2 _upHand = new Vector2(.6f, .15f);
    [SerializeField] private Vector2 _leftHand = new Vector2(-.65f, -.35f);
    [SerializeField] private Vector2 _rightHand = new Vector2(.65f, -.35f);
    [SerializeField] private PlayerSpannerView _view;
    [SerializeField] private PlayerSpannerRepair _repair;
    [Header("Impact Feedback")]
    [SerializeField] private PlayerSpannerSlash _slashPrefab;
    [SerializeField] private ParticleSystem _impactPrefab;
    [SerializeField, Range(0f, .1f)] private float _impactPauseSeconds = .045f;
    private readonly HashSet<EnemyHealth> _hit = new();
    private Transform _owner;
    private PlayerInputHandler _input;
    private PlayerWeaponRig _rig;
    private PlayerSpriteMotion _motion;
    private PlayerControlState _controls;
    private PlayerTurretRepair _heldRepair;
    private Vector2 _swingDirection;
    private float _swingRemaining;
    private double _nextAttackTime;
    private double GameSeconds => FieldSimulationTime.Seconds(this);
    private bool _applyingStrike;
    private double _impactAt;
    private bool _pendingStrike;
    private bool _repairIntent;
    private PlayerBaseStats _baseStats;
    public float AttackInterval => Mathf.Max(.05f, _attackInterval);
    public int AttackDamage => Mathf.Max(0, _damage);
    public float AttackRange => Mathf.Max(.1f, _range);

    public void Bind(Transform owner, PlayerInputHandler input)
    {
        _owner = owner;
        _input = input;
        _rig = owner != null ? owner.GetComponentInChildren<PlayerWeaponRig>(true) : null;
        _motion = owner != null ? owner.GetComponent<PlayerSpriteMotion>() : null;
        _controls = owner != null ? owner.GetComponent<PlayerControlState>() : null;
        _heldRepair = owner != null ? owner.GetComponent<PlayerTurretRepair>() : null;
        _baseStats = owner != null ? owner.GetComponent<PlayerBaseStats>() : null;
        if (_view == null) _view = GetComponentInChildren<PlayerSpannerView>(true);
        if (_repair == null) _repair = GetComponent<PlayerSpannerRepair>();
    }

    public bool TryAttack(Vector2 direction)
    {
        if (!isActiveAndEnabled || _owner == null || (_controls != null && !_controls.CanAct) ||
            (_heldRepair != null && _heldRepair.IsRepairing) ||
            !PlayerAimUtility.IsValidDirection(direction) || _pendingStrike || GameSeconds < _nextAttackTime)
            return false;
        _nextAttackTime = GameSeconds + AttackInterval;
        _swingRemaining = Mathf.Max(.01f, _swingSeconds);
        _swingDirection = direction.normalized;
        // R 상호작용을 가진 플레이어는 LMB 타격으로 체력을 회복하지 않습니다.
        _repairIntent = _heldRepair == null && _repair != null && _repair.CanRepair(_owner, _swingDirection);
        bool animated = _motion != null && _motion.PlayToolAction(_repairIntent);
        float delay = animated ? _motion.ToolImpactDelay(_repairIntent) : _swingSeconds * .5f;
        _impactAt = GameSeconds + Mathf.Max(0f, delay);
        _nextAttackTime = System.Math.Max(_nextAttackTime, _impactAt);
        _pendingStrike = true;
        return true;
    }

    private void Update()
    {
        if (!_pendingStrike || !FieldSimulationTime.CanAdvance(this)) return;
        if (_owner == null || (_controls != null && !_controls.CanAct))
        {
            _pendingStrike = false;
            return;
        }
        if (GameSeconds < _impactAt) return;
        _pendingStrike = false;
        if (_repairIntent)
        {
            if (_heldRepair == null && _repair != null && _repair.TryRepair(_owner, _swingDirection, out Vector3 point))
                ShowImpact(point, true);
            return;
        }
        _applyingStrike = true;
        try { ShowAttackSlash(); ApplyMeleeDamage(); }
        finally { _applyingStrike = false; }
    }

    private void ShowAttackSlash()
    {
        if (_slashPrefab == null) return;
        PlayerSpannerSlash slash = Instantiate(_slashPrefab);
        SceneManager.MoveGameObjectToScene(slash.gameObject, _owner.gameObject.scene);
        if (!slash.Show(_owner, _swingDirection, Mathf.Max(.1f, _range))) Destroy(slash.gameObject);
    }

    private void ApplyMeleeDamage()
    {
        _hit.Clear();
        bool feedbackShown = false;
        int damage = _baseStats != null ? Mathf.RoundToInt(_baseStats.ResolveWeaponDamage(_damage)) : Mathf.Max(0, _damage);
        // Query again at the strike: moving away or stepping behind a wall must avoid the hit.
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(_owner.position, Mathf.Max(.1f, _range)))
        {
            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.gameObject.scene != _owner.gameObject.scene ||
                enemy.CurrentHealth <= 0 || _hit.Contains(enemy)) continue;
            Vector2 point = collider.ClosestPoint(_owner.position);
            if (!PlayerSpannerTargeting.CanReach(_owner, enemy.transform, point, _swingDirection, _range, _arc)) continue;
            _hit.Add(enemy);
            int previousHealth = enemy.CurrentHealth;
            enemy.TryTakeDamage(damage, AttackSourceType.Player);
            // A client-side rejected request must not appear to be a confirmed hit.
            if (!feedbackShown && enemy.CurrentHealth < previousHealth)
            {
                ShowImpact(point, false);
                feedbackShown = true;
            }
        }
    }

    private void ShowImpact(Vector3 position, bool repairing)
    {
        if (_motion != null) _motion.PauseAtImpact(_impactPauseSeconds);
        if (_impactPrefab == null) return;
        position.z = _owner.position.z;
        ParticleSystem effect = Instantiate(_impactPrefab, position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(effect.gameObject, _owner.gameObject.scene);
        var main = effect.main;
        main.startColor = repairing ? new Color(.35f, 1f, .65f) : new Color(1f, .85f, .35f);
        var ownerRenderer = _owner.GetComponent<SpriteRenderer>();
        var effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
        if (ownerRenderer != null && effectRenderer != null)
        {
            effectRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            effectRenderer.sortingOrder = ownerRenderer.sortingOrder + 3;
        }
        effect.Play();
        Destroy(effect.gameObject, main.duration + main.startLifetime.constantMax + .1f);
    }

    private void LateUpdate()
    {
        if (_owner == null) return;
        Vector2 direction = _swingRemaining > 0f ? _swingDirection : (_input != null ? _input.AimDirection : Vector2.down);
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = Vector2.down;
        direction.Normalize();
        float duration = Mathf.Max(.01f, _swingSeconds);
        float progress = 1f - _swingRemaining / duration;
        float swing = _swingRemaining > 0f ? Mathf.Lerp(-_arc * .5f, _arc * .5f, progress) : 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + swing);
        PlayerAimFacing facing = _rig != null ? _rig.Facing : PlayerAimUtility.GetFacing(direction);
        // The authored body sprite owns the diagonal tool. The separate legacy
        // wrench uses its existing four hand anchors whenever it is displayed.
        if (PlayerAimUtility.IsDiagonal(facing)) facing = PlayerAimUtility.GetFacing(direction);
        transform.localPosition = RightHandPosition(facing);
        if (_view != null)
        {
            _view.SetBehindPlayer(facing == PlayerAimFacing.Up);
            _view.SetVisible(_motion == null || !_motion.OwnsDisplay);
        }
        _swingRemaining = Mathf.Max(0f, _swingRemaining - FieldSimulationTime.DeltaTime(this));
    }

    private Vector2 RightHandPosition(PlayerAimFacing facing)
    {
        switch (facing)
        {
            case PlayerAimFacing.Up: return _upHand;
            case PlayerAimFacing.Left: return _leftHand;
            case PlayerAimFacing.Right: return _rightHand;
            default: return _downHand;
        }
    }

    public void CancelPendingAttack()
    {
        _pendingStrike = false;
        _swingRemaining = 0f;
        if (_motion != null) _motion.CancelToolAction();
    }

    private void OnDisable()
    {
        CancelPendingAttack();
        if (_view != null) _view.SetVisible(true);
    }
}
