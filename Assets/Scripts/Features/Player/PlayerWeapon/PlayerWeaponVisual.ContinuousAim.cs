using UnityEngine;

public sealed partial class PlayerWeaponVisual
{
    [System.Serializable]
    private sealed class AimWeapon
    {
        public Sprite sprite;
        [Min(.1f)] public float length = 2f;
        public Vector2 grip = new Vector2(.4f, .4f);
        public Vector2 muzzle = new Vector2(.9f, .55f);
        public bool IsConfigured => sprite != null && length > 0f && !float.IsInfinity(length) &&
            !float.IsNaN(length) && IsFinite(grip) && IsFinite(muzzle);
        private static bool IsFinite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }

    [Header("Continuous mouse aim")]
    [SerializeField] private bool _continuousAim;
    [SerializeField] private Vector2 _aimMount = new Vector2(0f, .65f);
    [SerializeField, Min(0f)] private float _aimRadius = .35f;
    [SerializeField] private AimWeapon _aimDefaultGun = new AimWeapon();
    [SerializeField] private AimWeapon _aimCryoBlaster = new AimWeapon();
    [SerializeField] private AimWeapon _aimSniper = new AimWeapon();
    [SerializeField] private AimWeapon _aimSpanner = new AimWeapon();
    [Header("Spanner attack area")]
    [SerializeField, Min(1f)] private float _spannerAttackScale = 1.6f;
    [SerializeField, Min(0f)] private float _spannerBodyClearance = .12f;
    // The existing slash's first visible pixel is 89 px ahead of its pivot;
    // its authored outer radius is 178 px. Keep its visible area outside the body.
    private const float SpannerSlashInnerForwardRatio = .5f;
    private readonly System.Collections.Generic.Dictionary<Sprite, Vector2[]> _spannerBodyMeshes = new();
    private PlayerInputHandler _aimInput;
    private PlayerControlState _aimControls;
    private PlayerSkillController _aimSkills;
    private PlayerTurretRepair _aimRepair;
    [SerializeField] private SpriteRenderer _secondaryWeapon;
    [SerializeField] private PlayerCryoSpray _secondaryCryoSpray;
    [SerializeField, Min(0f)] private float _dualWieldSpacing = .45f;
    private PlayerDualWieldSkill _dualWield;
    public bool IsDualWieldConfigured => _continuousAim && IsConfigured && _secondaryWeapon != null && _secondaryCryoSpray != null && _secondaryCryoSpray.IsConfigured;
    public PlayerCryoSpray SecondaryCryoSpray => _secondaryCryoSpray;
    public bool IsDualWieldVisible => IsDualWieldConfigured && _dualWield != null && _dualWield.IsActive &&
        _selection.CurrentWeapon != WeaponType.Spanner && !_motion.IsHoldingRepair &&
        (_aimControls == null || !_aimControls.IsInSpaceship) && _weapon.enabled;

    public bool UsesContinuousAim => _continuousAim;
    // The authored mount is for the left-facing body. Mirror its offset about
    // the fixed foot anchor together with the body, without moving the actor.
    private Vector2 AimMount
    {
        get
        {
            Vector2 mount = _aimMount;
            if (_motion != null && _motion.UsesSpaceManBody && _body != null && _body.flipX)
                mount.x = -mount.x;
            return mount;
        }
    }
    private bool IsContinuousAimConfigured => _aimDefaultGun != null && _aimDefaultGun.IsConfigured &&
        _aimCryoBlaster != null && _aimCryoBlaster.IsConfigured &&
        _aimSniper != null && _aimSniper.IsConfigured && _aimSpanner != null && _aimSpanner.IsConfigured;
    private AimWeapon CurrentAimWeapon => _motion != null && _motion.IsHoldingRepair ? _aimSpanner :
        _selection.CurrentWeapon switch
        {
            WeaponType.CryoBlaster => _aimCryoBlaster,
            WeaponType.Sniper => _aimSniper,
            WeaponType.Spanner => _aimSpanner,
            _ => _aimDefaultGun
        };

    private void InitializeContinuousAim()
    {
        _aimInput = GetComponent<PlayerInputHandler>();
        _aimControls = GetComponent<PlayerControlState>();
        _aimSkills = GetComponent<PlayerSkillController>();
        _aimRepair = GetComponent<PlayerTurretRepair>();
        _dualWield = GetComponent<PlayerDualWieldSkill>();
    }

    public bool TryGetContinuousAimDirection(out Vector2 direction)
    {
        direction = Vector2.down;
        if (!_continuousAim || !isActiveAndEnabled || !IsConfigured || _aimInput == null) return false;
        // Shots can query the mount before body LateUpdate. Resolve its flip
        // first so that this frame's projectile and displayed muzzle agree.
        _motion.RefreshSpaceManBody();
        bool canFace = (_aimControls == null || _aimControls.CanMove) &&
            (_aimSkills == null || !_aimSkills.IsSelectionOpen);
        if (!canFace) return true;
        if (_aimRepair != null && _aimRepair.TryGetFacingDirection(out var repairing))
            direction = repairing;
        else if (_selection.CurrentWeapon == WeaponType.Spanner && _selection.Spanner != null &&
            _selection.Spanner.TryGetSwingDirection(out var swing))
            direction = swing;
        else if (_aimInput.HasAimTarget)
        {
            direction = _aimInput.MouseWorldPosition - (Vector2)transform.TransformPoint(AimMount);
        }
        else direction = _aimInput.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = _aimInput.AimDirection;
        if (!PlayerAimUtility.IsValidDirection(direction)) direction = Vector2.down;
        direction.Normalize();
        return true;
    }

    private static Vector2 SpritePoint(Sprite sprite, Vector2 normalized, bool flipY)
    {
        Vector2 point = (normalized * sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
        if (flipY) point.y = -point.y;
        return point;
    }

    /// <summary>확대한 검기의 보이는 부분과 타격 범위를 몸체 바깥에 배치합니다.</summary>
    public bool TryGetSpannerStrikeGeometry(Vector2 worldDirection, out Vector2 origin, out float range)
        => TryGetSpannerStrikeGeometry(worldDirection, out origin, out range, out _);

    public bool TryGetSpannerStrikeGeometry(Vector2 worldDirection, out Vector2 origin, out float range,
        out float minimumForward)
    {
        origin = default; range = 0f; minimumForward = 0f;
        if (!_continuousAim || !isActiveAndEnabled || _aimSpanner == null || !_aimSpanner.IsConfigured ||
            _body == null || _body.sprite == null || !PlayerAimUtility.IsValidDirection(worldDirection)) return false;
        worldDirection.Normalize();
        Vector2 direction = transform.InverseTransformDirection(worldDirection).normalized;
        origin = transform.TransformPoint(AimMount + direction * _aimRadius);
        Vector2 head = SpritePoint(_aimSpanner.sprite, _aimSpanner.muzzle, direction.x < 0f);
        Vector2 grip = SpritePoint(_aimSpanner.sprite, _aimSpanner.grip, direction.x < 0f);
        var rotation = Quaternion.FromToRotation(Vector3.right, direction);
        range = transform.TransformVector(rotation * ((Vector3)(head - grip) *
            (_aimSpanner.length / _aimSpanner.sprite.bounds.size.x))).magnitude * Mathf.Max(1f, _spannerAttackScale);
        if (range <= .01f || float.IsNaN(range) || float.IsInfinity(range)) return false;
        minimumForward = range * SpannerSlashInnerForwardRatio;
        if (!_spannerBodyMeshes.TryGetValue(_body.sprite, out var vertices))
        {
            vertices = _body.sprite.vertices;
            _spannerBodyMeshes.Add(_body.sprite, vertices);
        }
        float bodyFront = float.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            Vector2 point = vertex;
            if (_body.flipX) point.x = -point.x;
            if (_body.flipY) point.y = -point.y;
            Vector2 worldPoint = _body.transform.TransformPoint(point);
            bodyFront = Mathf.Max(bodyFront, Vector2.Dot(worldPoint - origin, worldDirection));
        }
        if (vertices.Length > 0)
        {
            float clearance = transform.TransformVector(direction * _spannerBodyClearance).magnitude;
            origin += worldDirection * (bodyFront + clearance - minimumForward);
        }
        return true;
    }

    private void RefreshContinuousAim()
    {
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled ||
            (_aimControls != null && _aimControls.IsInSpaceship) ||
            !TryGetContinuousAimDirection(out var worldDirection))
        { Hide(); return; }

        var art = CurrentAimWeapon;
        Vector2 direction = transform.InverseTransformDirection(worldDirection).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // A leftward gun rotates 180 degrees, then flips vertically to keep the
        // stock, sights and magazine upright. Body flip never rotates the gun.
        bool flipped = direction.x < 0f;
        Vector2 gripPoint = art.grip;
        if ((_selection.CurrentWeapon == WeaponType.Spanner || _motion.IsHoldingRepair) && _motion.IsPlayingToolAction)
        {
            float phase = _motion.ToolActionProgress;
            float halfArc = (_selection.Spanner != null ? _selection.Spanner.AttackArc : 100f) * .5f;
            float swing;
            if (_motion.IsHoldingRepair)
                swing = phase < .5f ? Mathf.Lerp(halfArc, 0f, phase * 2f) : Mathf.Lerp(0f, halfArc, (phase - .5f) * 2f);
            else
            {
                // Wind up, cross the aim line exactly at impact (50%), follow
                // through, then recover. Rotate about the handle, not the head.
                float arc = Mathf.Max(halfArc, 85f);
                if (phase < .2f) swing = Mathf.SmoothStep(0f, arc, phase / .2f);
                else if (phase < .68f) swing = Mathf.Lerp(arc, -arc * .6f, (phase - .2f) / .48f);
                else swing = Mathf.SmoothStep(-arc * .6f, 0f, (phase - .68f) / .32f);
                float reach = phase < .2f ? Mathf.SmoothStep(0f, 1f, phase / .2f) :
                    phase < .68f ? 1f : Mathf.SmoothStep(1f, 0f, (phase - .68f) / .32f);
                gripPoint.x = Mathf.Lerp(art.grip.x, .22f, reach);
            }
            angle += flipped ? -swing : swing;
        }
        var rotation = Quaternion.Euler(0f, 0f, angle);
        float scale = art.length / art.sprite.bounds.size.x;
        Vector3 grip = SpritePoint(art.sprite, gripPoint, flipped) * scale;
        Vector3 mount = AimMount + direction * _aimRadius;
        _weapon.sprite = art.sprite;
        _weapon.transform.localScale = new Vector3(scale, scale, 1f);
        _weapon.transform.localRotation = rotation;
        _weapon.transform.localPosition = mount - rotation * grip;
        _weapon.flipX = false;
        _weapon.flipY = flipped;
        _weapon.color = _body.color;
        _weapon.sortingLayerID = _body.sortingLayerID;
        // The weapon covers the body hands within the actor SortingGroup.
        _weapon.sortingOrder = _body.sortingOrder + 1;
        _sortingGroup.sortingLayerID = _body.sortingLayerID;
        _sortingGroup.sortingOrder = _body.sortingOrder;
        _weapon.enabled = true;
        RefreshSecondaryWeapon(direction, art, rotation, grip, mount, flipped);
        if (_foregroundHands != null) { _foregroundHands.enabled = false; _foregroundHands.sprite = null; }
        RefreshRepairSorting();
        RefreshRepairImpact();
    }

    // 시전 연출의 마지막 단계에서만 덮어씁니다. 사격 시작은 연출을 먼저 취소합니다.
    internal void ApplySkillCastPose(float blend, Vector2 gripPosition)
    {
        if (!_continuousAim || !IsConfigured || !_weapon.enabled) return;
        var art = CurrentAimWeapon;
        float currentAngle = _weapon.transform.localEulerAngles.z;
        float restingAngle = _body.flipX ? -40f : -140f;
        // 살짝 내리는 동작입니다. 긴 총의 개머리판이 얼굴을 가리지 않도록 회전은 18도로 제한합니다.
        float targetAngle = currentAngle + Mathf.Clamp(Mathf.DeltaAngle(currentAngle, restingAngle), -18f, 18f);
        var rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(currentAngle, targetAngle, blend));
        Vector3 grip = SpritePoint(art.sprite, art.grip, _weapon.flipY) * _weapon.transform.localScale.x;
        Vector3 currentGrip = _weapon.transform.localPosition + _weapon.transform.localRotation * grip;
        _weapon.transform.localRotation = rotation;
        _weapon.transform.localPosition = Vector3.Lerp(currentGrip, gripPosition, blend) - rotation * grip;
        if (_secondaryWeapon != null && _secondaryWeapon.enabled)
        {
            _secondaryWeapon.transform.localRotation = rotation;
            _secondaryWeapon.transform.localPosition = _weapon.transform.localPosition + Vector3.left * _dualWieldSpacing;
        }
    }

    private void RefreshSecondaryWeapon(Vector2 direction, AimWeapon art, Quaternion rotation, Vector3 grip, Vector3 mount, bool flipped)
    {
        if (_secondaryWeapon == null) return;
        _secondaryWeapon.enabled = IsDualWieldVisible;
        if (!_secondaryWeapon.enabled) return;
        // Keep the original gun in its normal pose and tuck the second hand
        // just to its left. Both barrels still converge on the same pointer.
        Vector3 offset = Vector3.left * _dualWieldSpacing;
        _secondaryWeapon.sprite = art.sprite;
        _secondaryWeapon.transform.localScale = _weapon.transform.localScale;
        _secondaryWeapon.transform.localRotation = rotation;
        _secondaryWeapon.transform.localPosition = mount + offset - rotation * grip;
        _secondaryWeapon.flipX = false; _secondaryWeapon.flipY = flipped;
        _secondaryWeapon.color = _weapon.color;
        _secondaryWeapon.sortingLayerID = _body.sortingLayerID;
        _secondaryWeapon.sortingOrder = _body.sortingOrder - 1;
        if (_aimInput != null && _aimInput.HasAimTarget)
        {
            Vector2 hand = transform.TransformPoint(mount + offset);
            Vector2 toTarget = _aimInput.MouseWorldPosition - hand;
            if (PlayerAimUtility.IsValidDirection(toTarget))
            {
                Vector2 localDirection = transform.InverseTransformDirection(toTarget).normalized;
                bool ownFlip = localDirection.x < 0f;
                Vector3 ownGrip = SpritePoint(art.sprite, art.grip, ownFlip) * _weapon.transform.localScale.x;
                var ownRotation = Quaternion.Euler(0, 0, Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg);
                _secondaryWeapon.flipY = ownFlip;
                _secondaryWeapon.transform.localRotation = ownRotation;
                _secondaryWeapon.transform.localPosition = mount + offset - ownRotation * ownGrip;
            }
        }
    }

    public bool TryGetSecondaryMuzzle(out Vector3 position, out Vector2 direction)
    {
        position = default; direction = default;
        if (!IsDualWieldConfigured) return false;
        RefreshContinuousAim();
        if (!IsDualWieldVisible || !_secondaryWeapon.enabled) return false;
        var art = CurrentAimWeapon;
        position = _secondaryWeapon.transform.TransformPoint(SpritePoint(art.sprite, art.muzzle, _secondaryWeapon.flipY));
        direction = _aimInput != null && _aimInput.HasAimTarget ? _aimInput.MouseWorldPosition - (Vector2)position : (Vector2)_secondaryWeapon.transform.right;
        if (!PlayerAimUtility.IsValidDirection(direction)) return false;
        direction.Normalize(); return true;
    }

    private bool TryGetContinuousMuzzlePosition(out Vector3 position)
    {
        position = default;
        if (!isActiveAndEnabled || !IsConfigured || !_motion.OwnsDisplay || !_body.enabled) return false;
        // Rig Update and the actual first shot may run before visual LateUpdate.
        RefreshContinuousAim();
        if (!_weapon.enabled) return false;
        var art = CurrentAimWeapon;
        position = _weapon.transform.TransformPoint(SpritePoint(art.sprite, art.muzzle, _weapon.flipY));
        return true;
    }
}
