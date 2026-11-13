using UnityEngine;

public sealed partial class PlayerSpriteMotion
{
    [Header("Optional Space Man body (weapon rotates separately)")]
    [SerializeField] private bool _useSpaceManBody;
    [SerializeField] private Sprite[] _spaceManSide;
    [SerializeField, Min(0f)] private float _spaceManIdleFps = 5f;
    [SerializeField, Min(0f)] private float _spaceManMoveFps = 10f;
    [SerializeField] private Transform _spaceManShadow;
    [SerializeField] private Vector3 _spaceManShadowPosition = new Vector3(.17f, .2f, .01f);
    private PlayerDirectionalColliders _spaceManColliders;
    private SpriteRenderer _spaceManShadowRenderer;
    private float _spaceManPhase;
    private bool _spaceManFacingRight;

    public bool UsesSpaceManBody => _useSpaceManBody && HasFrames(_spaceManSide);
    public PlayerAimFacing SpaceManFacing => _spaceManFacingRight ? PlayerAimFacing.Right : PlayerAimFacing.Left;
    public float ToolActionProgress => IsPlayingToolAction
        ? Mathf.Clamp01(_elapsed / Mathf.Max(.01f, CurrentClip.length)) : 0f;

    // Animator still owns the existing action clock and impact pause. Substitute
    // only the displayed body after evaluation; never animate the physics root.
    private void LateUpdate()
    {
        if (!_ownsDisplay || !UsesSpaceManBody) return;
        bool canMove = _controls == null || _controls.CanMove;
        if (canMove && _impactPauseRemaining <= 0f)
            _spaceManPhase = Mathf.Repeat(_spaceManPhase + Time.deltaTime *
                (HasMovementInput ? _spaceManMoveFps : _spaceManIdleFps), 1000f);
        RefreshSpaceManBody();
    }

    public void RefreshSpaceManBody()
    {
        if (!_ownsDisplay || !UsesSpaceManBody || _renderer == null) return;
        Vector2 direction = _input != null ? _input.AimDirection : Vector2.down;
        if (_heldRepair && _repair != null && _repair.TryGetFacingDirection(out var repairing))
            direction = repairing;
        bool canFace = (_controls == null || _controls.CanMove) &&
            (_skills == null || !_skills.IsSelectionOpen);
        // Vertical aim keeps the last horizontal facing, independent of Animator flips.
        if (canFace && Mathf.Abs(direction.x) > .01f) _spaceManFacingRight = direction.x > 0f;
        _renderer.sprite = _spaceManSide[Mathf.FloorToInt(_spaceManPhase) % _spaceManSide.Length];
        // The source sheet side row faces left. Only the body mirrors; the gun
        // has its own continuous rotation and vertical flip.
        _renderer.flipX = _spaceManFacingRight;
        _renderer.flipY = false;
        if (_spaceManColliders != null) _spaceManColliders.SetFacing(SpaceManFacing);
        // Legacy clips still animate this child. Replace their old contact point
        // after evaluation, without moving the actor or its physics shapes.
        if (_spaceManShadow != null)
        {
            Vector3 ground = _spaceManShadowPosition;
            if (_spaceManFacingRight) ground.x = -ground.x;
            // The existing shadow prefab has a visual child offset; preserve it
            // for authored poses and place the visible ellipse at the contact point.
            _spaceManShadow.localPosition = ground - (_spaceManShadowRenderer != null
                ? _spaceManShadowRenderer.transform.localPosition : Vector3.zero);
            _spaceManShadow.localRotation = Quaternion.identity;
            _spaceManShadow.localScale = Vector3.one;
        }
    }

    private static bool HasFrames(Sprite[] frames)
    {
        if (frames == null || frames.Length != 5) return false;
        foreach (var frame in frames) if (frame == null) return false;
        return true;
    }
}
