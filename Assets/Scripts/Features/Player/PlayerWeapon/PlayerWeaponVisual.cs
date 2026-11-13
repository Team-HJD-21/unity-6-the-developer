using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>몸체 프레임에 무기를 맞추고, 외형에 지정된 총구 좌표를 Weapon Rig에 제공합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed partial class PlayerWeaponVisual : MonoBehaviour, IPlayerPresentationRandomBinding
{
    [Header("Owner and authored renderers")]
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private SpriteRenderer _weapon;
    [SerializeField] private SpriteRenderer _foregroundHands;
    [SerializeField] private SortingGroup _sortingGroup;
    [SerializeField] private SortingGroup _repairSortingGroup;
    [SerializeField] private ParticleSystem _repairImpactPrefab;
    [SerializeField] private PlayerWeaponSelection _selection;
    [SerializeField] private PlayerSpriteMotion _motion;
    [Header("Replace appearances here to change the equipped weapon art")]
    [SerializeField] private PlayerWeaponAppearance _defaultGun;
    [SerializeField] private PlayerWeaponAppearance _cryoBlaster;
    [SerializeField] private PlayerWeaponAppearance _spanner;
    [SerializeField] private PlayerWeaponAppearance _sniper;
    private Sprite _lastMissingFrame;
    private Transform _repairSortingTarget;
    private SortingGroup _repairTargetGroup;
    private SortingGroup[] _repairTargetParts;
    private uint _lastRepairImpactSequence;
    private IRandomSource _repairImpactRandom;
    private IDisposable _repairRandomLease;

    public void BindPresentationRandom(IRandomStreams streams)
    {
        UnbindPresentationRandom();
        _repairRandomLease = RandomBinding.Bind(streams,
            "vfx/player-repair-impact", ref _repairImpactRandom);
    }

    public void UnbindPresentationRandom()
    {
        _repairRandomLease?.Dispose();
        _repairRandomLease = null;
        _repairImpactRandom = null;
    }

    private void OnDestroy() => UnbindPresentationRandom();

    public bool IsConfigured => _body != null && _weapon != null && _body != _weapon &&
        _foregroundHands != null && _foregroundHands != _body && _foregroundHands != _weapon &&
        _body.transform == transform && _weapon.transform.parent == transform &&
        _foregroundHands.transform.parent == transform &&
        _sortingGroup != null && _sortingGroup.transform == transform &&
        _selection != null && _selection.transform == transform && _motion != null && _motion.transform == transform &&
        (_continuousAim ? IsContinuousAimConfigured : _defaultGun != null && _defaultGun.IsConfigured &&
        _cryoBlaster != null && _cryoBlaster.IsConfigured && _spanner != null && _spanner.IsConfigured &&
        _sniper != null && _sniper.IsConfigured);
    public SpriteRenderer Renderer => _weapon;
    public SpriteRenderer ForegroundHandsRenderer => _foregroundHands;

    private PlayerWeaponAppearance CurrentAppearance
    {
        get
        {
            if (_motion != null && _motion.IsHoldingRepair) return _spanner;
            switch (_selection.CurrentWeapon)
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
        InitializeContinuousAim();
        if (!IsConfigured)
            Debug.LogWarning("[PlayerWeaponVisual] Connect the body, weapon and foreground-hand renderers, sorting group and all weapon appearances.", this);
        Hide();
    }

    // Animator evaluates the body before LateUpdate, including turns and impact pauses.
    private void LateUpdate() => Refresh();

    public bool TryGetMuzzlePosition(out Vector3 position, bool forShot = false)
    {
        if (_continuousAim) return TryGetContinuousMuzzlePosition(out position);
        position = default;
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled ||
            (_aimControls != null && _aimControls.IsInSpaceship) ||
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
        if (_continuousAim) { RefreshContinuousAim(); return; }
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled ||
            (_aimControls != null && _aimControls.IsInSpaceship))
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
        _weapon.transform.localScale = Vector3.one;
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
        RefreshForegroundHands(frame.ForegroundHands);
        RefreshRepairSorting();
        RefreshRepairImpact();
    }

    private void RefreshForegroundHands(Sprite sprite)
    {
        // Hand cutouts share the body's complete canvas and stay at its origin.
        // A separate authored layer lets fingers cover a front-facing weapon
        // while rear-facing weapons remain behind the unmodified body sprite.
        _foregroundHands.sprite = sprite;
        _foregroundHands.transform.localPosition = Vector3.zero;
        _foregroundHands.transform.localRotation = Quaternion.identity;
        _foregroundHands.transform.localScale = Vector3.one;
        _foregroundHands.flipX = _body.flipX;
        _foregroundHands.flipY = _body.flipY;
        _foregroundHands.color = _body.color;
        _foregroundHands.sortingLayerID = _body.sortingLayerID;
        _foregroundHands.sortingOrder = _body.sortingOrder + 2;
        _foregroundHands.enabled = sprite != null;
    }

    private void Hide()
    {
        _lastRepairImpactSequence = _motion != null ? _motion.HeldRepairImpactSequence : 0;
        if (_repairSortingGroup != null) _repairSortingGroup.enabled = false;
        if (_weapon != null) _weapon.enabled = false;
        if (_foregroundHands != null) _foregroundHands.enabled = false;
    }

    private void OnDisable() => Hide();

    private void RefreshRepairImpact()
    {
        uint sequence = _motion.HeldRepairImpactSequence;
        bool struck = sequence != _lastRepairImpactSequence;
        _lastRepairImpactSequence = sequence;
        if (!struck || _repairImpactPrefab == null || _aimRepair == null || !_aimRepair.isActiveAndEnabled ||
            !_aimRepair.IsRepairing || _aimRepair.Target == null || !_motion.IsHoldingRepair ||
            _repairImpactRandom == null || !_repairImpactRandom.IsAvailable ||
            (_aimControls != null && !_aimControls.CanAct) ||
            !TeamHJD.Game.Presentation.Scene.FieldSimulationTime.CanAdvance(this)) return;

        Vector3 point = _continuousAim
            ? _weapon.transform.TransformPoint(SpritePoint(_aimSpanner.sprite, _aimSpanner.muzzle, _weapon.flipY))
            : _weapon.bounds.ClosestPoint(_aimRepair.Target.transform.position);
        point.z = transform.position.z;
        uint seed = (uint)_repairImpactRandom.NextInt(1, int.MaxValue);
        var effect = Instantiate(_repairImpactPrefab, point, Quaternion.identity);
        effect.useAutoRandomSeed = false;
        effect.randomSeed = seed;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect.gameObject, gameObject.scene);
        var renderer = effect.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            bool separate = _repairSortingGroup != null && _repairSortingGroup.enabled;
            renderer.sortingLayerID = separate ? _repairSortingGroup.sortingLayerID : _body.sortingLayerID;
            renderer.sortingOrder = Mathf.Min(short.MaxValue,
                (separate ? _repairSortingGroup.sortingOrder : _sortingGroup.sortingOrder) + 1);
        }
        effect.Play();
    }

    private void RefreshRepairSorting()
    {
        if (_repairSortingGroup == null) return;
        bool repairing = _aimRepair != null && _aimRepair.isActiveAndEnabled && _aimRepair.IsRepairing &&
            _aimRepair.Target != null && _motion.IsHoldingRepair;
        _repairSortingGroup.enabled = repairing;
        if (!repairing) return;

        // Only the tool leaves the actor group while repairing. The body keeps
        // its normal world depth, even when the turret is in front of it.
        int layer = _sortingGroup.sortingLayerID;
        int order = _sortingGroup.sortingOrder;
        if (_repairSortingTarget != _aimRepair.Target.transform)
        {
            _repairSortingTarget = _aimRepair.Target.transform;
            _repairTargetParts = _repairSortingTarget.GetComponentsInChildren<SortingGroup>(true);
            _repairTargetGroup = _repairSortingTarget.GetComponentInParent<SortingGroup>();
            while (_repairTargetGroup != null && !_repairTargetGroup.sortAtRoot)
            {
                var parent = _repairTargetGroup.transform.parent;
                var outer = parent != null ? parent.GetComponentInParent<SortingGroup>() : null;
                if (outer == null || !outer.isActiveAndEnabled) break;
                _repairTargetGroup = outer;
            }
        }
        IncludeRepairSorting(_repairTargetGroup, ref layer, ref order);
        // Detached barrels use sortAtRoot and are sorted independently of the
        // turret body. Keep the tool above those parts as their depth changes.
        foreach (var part in _repairTargetParts)
            if (part != null && part.sortAtRoot) IncludeRepairSorting(part, ref layer, ref order);
        _repairSortingGroup.sortAtRoot = true;
        _repairSortingGroup.sortingLayerID = layer;
        _repairSortingGroup.sortingOrder = Mathf.Min(order + 1, short.MaxValue);
    }

    private static void IncludeRepairSorting(SortingGroup group, ref int layer, ref int order)
    {
        if (group == null || !group.isActiveAndEnabled) return;
        int groupLayer = SortingLayer.GetLayerValueFromID(group.sortingLayerID);
        int currentLayer = SortingLayer.GetLayerValueFromID(layer);
        if (groupLayer > currentLayer || (groupLayer == currentLayer && group.sortingOrder > order))
        {
            layer = group.sortingLayerID;
            order = group.sortingOrder;
        }
    }
}
