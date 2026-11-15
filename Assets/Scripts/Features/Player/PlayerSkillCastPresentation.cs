using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>스킬 확정 후 손목 패널을 조작하고 목표를 가리킵니다. 전투·이동 시간은 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
// 총구 앵커(290)가 기본 조준 자세를 동기화한 다음 최종 표시 자세를 적용합니다.
[DefaultExecutionOrder(300)]
public sealed class PlayerSkillCastPresentation : MonoBehaviour
{
    [SerializeField] private Sprite _bodyWithoutArms;
    [SerializeField] private SpriteRenderer[] _upperArms = new SpriteRenderer[2];
    [SerializeField] private SpriteRenderer[] _forearms = new SpriteRenderer[2];
    [SerializeField] private SpriteRenderer _panel;
    [SerializeField, Range(.3f, .4f)] private float _duration = .36f;
    private PlayerSpriteMotion _motion;
    private SpriteRenderer _body;
    private PlayerWeaponVisual _weapon;
    private PlayerSkillController _skills;
    private PlayerControlState _controls;
    private PlayerInfo _info;
    private Vector3 _target;
    private float _elapsed;
    private double _startedAt;
    public bool IsPlaying { get; private set; }
    public float Progress => IsPlaying ? Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _duration)) : 0;
    public bool IsConfigured => _bodyWithoutArms != null && _panel != null &&
        ValidArms(_upperArms) && ValidArms(_forearms);

    private static bool ValidArms(SpriteRenderer[] arms) => arms != null && arms.Length == 2 &&
        arms[0] != null && arms[1] != null && arms[0].sprite != null && arms[1].sprite != null;

    private void Awake()
    {
        _motion = GetComponent<PlayerSpriteMotion>();
        _body = GetComponent<SpriteRenderer>();
        _weapon = GetComponent<PlayerWeaponVisual>();
        _skills = GetComponent<PlayerSkillController>();
        _controls = GetComponent<PlayerControlState>();
        _info = GetComponent<PlayerInfo>();
    }

    private void OnEnable()
    {
        if (_skills != null) _skills.CastPerformed += OnCast;
        HideArms();
    }

    private void OnCast(PlayerSkillId skill, Vector3 target)
    {
        if (!IsConfigured || !isActiveAndEnabled || _motion == null || !_motion.OwnsDisplay ||
            !_motion.UsesSpaceManBody || (_controls != null && !_controls.CanAct)) return;
        _target = target;
        _elapsed = 0;
        _startedAt = FieldSimulationTime.Seconds(this);
        IsPlaying = true;
    }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        if ((_controls != null && !_controls.CanAct && !_controls.IsFieldPaused) ||
            (_info != null && (_info.IsDestroyed || _info.curHp <= 0)) ||
            _motion == null || !_motion.OwnsDisplay || !_body.enabled ||
            (_skills != null && _skills.IsSelectionOpen))
        { Cancel(); return; }
        _elapsed = Mathf.Max(0, (float)(FieldSimulationTime.Seconds(this) - _startedAt));
        if (_elapsed >= _duration) { Cancel(); return; }
        RenderPose(Progress);
    }

    private static float Ease(float from, float to, float value) =>
        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));

    private void RenderPose(float phase)
    {
        float mirror = _body.flipX ? -1 : 1;
        Vector2 Mirror(Vector2 point) => new(point.x * mirror, point.y);
        float lower = Ease(0, .17f, phase) * (1 - Ease(.78f, 1, phase));
        float touch = Ease(.12f, .36f, phase);
        float reach = Ease(.52f, .76f, phase);
        float recover = Ease(.78f, 1, phase);
        var grip = Mirror(new Vector2(-.05f, .60f));
        var shoulder = Mirror(new Vector2(-.65f, .96f));
        var resting = Mirror(new Vector2(-.65f, .48f));
        Vector2 aim = transform.InverseTransformVector(_target - transform.position);
        if (!PlayerAimUtility.IsValidDirection(aim)) aim = new Vector2(-mirror, 0);
        Vector2 wrist = Vector2.Lerp(resting, grip + Mirror(new Vector2(-.08f, .07f)), touch);
        wrist = Vector2.Lerp(wrist, shoulder + aim.normalized * .95f, reach);
        wrist = Vector2.Lerp(wrist, resting, recover);
        _body.sprite = _bodyWithoutArms;
        DrawArm(0, Mirror(new Vector2(.08f, .97f)),
            Vector2.Lerp(Mirror(new Vector2(.08f, .48f)), grip, lower), -.12f);
        DrawArm(1, shoulder, wrist, .16f);
        _panel.enabled = phase >= .20f && phase < .60f;
        _panel.transform.localPosition = grip + new Vector2(0, .07f);
        _panel.transform.localRotation = Quaternion.Euler(0, 0, 12 * mirror);
        _panel.color = Color.Lerp(new Color(.34f, .85f, .92f), Color.white,
            .25f * (1 + Mathf.Sin(phase * 45))) * _body.color;
        _panel.sortingLayerID = _body.sortingLayerID;
        _panel.sortingOrder = _body.sortingOrder + 5;
        _weapon?.ApplySkillCastPose(lower, grip);
    }

    private void DrawArm(int index, Vector2 shoulder, Vector2 wrist, float bend)
    {
        Vector2 elbow = (shoulder + wrist) * .5f + new Vector2(0, -Mathf.Abs(bend));
        DrawBone(_upperArms[index], shoulder, elbow, index);
        DrawBone(_forearms[index], elbow, wrist, index);
    }

    private void DrawBone(SpriteRenderer bone, Vector2 start, Vector2 end, int index)
    {
        Vector2 delta = end - start;
        bone.enabled = true;
        bone.transform.localPosition = (start + end) * .5f;
        bone.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        bone.transform.localScale = new Vector3(delta.magnitude / bone.sprite.bounds.size.x, 1, 1);
        bone.color = _body.color * (index == 0 ? new Color(.82f, .82f, .82f, 1) : Color.white);
        bone.sortingLayerID = _body.sortingLayerID;
        bone.sortingOrder = _body.sortingOrder + (index == 0 ? -1 : 4);
    }

    public void Cancel()
    {
        bool restore = IsPlaying;
        IsPlaying = false;
        HideArms();
        if (!restore) return;
        if (_motion != null && _motion.isActiveAndEnabled) _motion.RefreshSpaceManBody();
        if (_weapon != null && _weapon.isActiveAndEnabled) _weapon.Refresh();
    }

    private void HideArms()
    {
        foreach (var arms in new[] { _upperArms, _forearms })
            if (arms != null) foreach (var arm in arms) if (arm != null) arm.enabled = false;
        if (_panel != null) _panel.enabled = false;
    }

    private void OnDisable()
    {
        if (_skills != null) _skills.CastPerformed -= OnCast;
        Cancel();
    }
}
