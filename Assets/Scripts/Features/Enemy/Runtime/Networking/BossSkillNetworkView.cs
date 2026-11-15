using Unity.Netcode;
using UnityEngine;

public enum BossSkill : byte { Roar, Stomp }

/// <summary>서버가 정한 보스 특수 모션과 원형 예고를 전달한다. 피해 판정은 능력이 담당한다.</summary>
[RequireComponent(typeof(Animator))]
public sealed class BossSkillNetworkView : NetworkBehaviour
{
    private static readonly int RoarHash = Animator.StringToHash("Roar");
    private static readonly int StompHash = Animator.StringToHash("Stomp");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private const int CirclePoints = 64;

    [SerializeField] private Animator animator;
    [SerializeField] private LineRenderer warningCircle;
    [SerializeField] private MeshRenderer warningFill;
    [SerializeField] private Color warningFillColor = new(1f, 0f, 0f, 0.22f);
    [SerializeField] private float warningZ;
    [SerializeField] private LineRenderer chargeWarning;
    [SerializeField] private BossGroundRuptureVisual groundRupture;

    private static readonly int ColorHash = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock _fillProperties;

    // x·y는 중심, z는 반경이다. 반경 0은 숨김이며 늦게 접속한 관전자도 현재 예고를 읽는다.
    private readonly NetworkVariable<Vector3> _warning = new();
    // 시작 XY와 도착 XY를 전달한다. 원점에서 길이 0인 값은 숨김이다.
    private readonly NetworkVariable<Vector4> _chargeWarning = new();

    public bool IsConfigured => warningCircle != null && warningCircle.sharedMaterial != null &&
                                warningFill != null && warningFill.sharedMaterial != null;
    public bool IsChargeConfigured => chargeWarning != null && chargeWarning.sharedMaterial != null;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (warningCircle == null) return;
        warningCircle.useWorldSpace = true;
        warningCircle.loop = true;
        warningCircle.positionCount = CirclePoints;
        warningCircle.startColor = Color.red;
        warningCircle.endColor = Color.red;
        warningCircle.enabled = false;
        if (chargeWarning != null)
        {
            chargeWarning.useWorldSpace = true;
            chargeWarning.positionCount = 2;
            chargeWarning.enabled = false;
        }
        if (warningFill != null)
        {
            _fillProperties = new MaterialPropertyBlock();
            _fillProperties.SetColor(ColorHash, warningFillColor);
            warningFill.SetPropertyBlock(_fillProperties);
            warningFill.enabled = false;
        }
    }

    /// <summary>보스가 밀려도 채움 원은 예고한 월드 위치와 반경을 유지한다.</summary>
    private void LateUpdate()
    {
        if (warningFill != null && warningFill.enabled)
            UpdateFillTransform(_warning.Value);
    }

    public override void OnNetworkSpawn()
    {
        _warning.OnValueChanged += OnWarningChanged;
        _chargeWarning.OnValueChanged += OnChargeWarningChanged;
        DrawWarning(_warning.Value);
        DrawChargeWarning(_chargeWarning.Value);
    }

    public override void OnNetworkDespawn()
    {
        _warning.OnValueChanged -= OnWarningChanged;
        _chargeWarning.OnValueChanged -= OnChargeWarningChanged;
        if (warningCircle != null) warningCircle.enabled = false;
        if (warningFill != null) warningFill.enabled = false;
        if (chargeWarning != null) chargeWarning.enabled = false;
        if (groundRupture != null) groundRupture.Clear();
        base.OnNetworkDespawn();
    }

    /// <summary>서버에서 한 번 재생한 뒤 다른 클라이언트에 동일 모션과 방향을 전달한다.</summary>
    public void PlaySkill(BossSkill skill)
    {
        if (!IsSpawned || !IsServer) return;
        float x = animator.GetFloat(MoveXHash);
        float y = animator.GetFloat(MoveYHash);
        ApplySkill(skill, x, y);
        PlaySkillRpc(skill, x, y);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlaySkillRpc(BossSkill skill, float moveX, float moveY)
    {
        if (!IsServer) ApplySkill(skill, moveX, moveY);
    }

    private void ApplySkill(BossSkill skill, float moveX, float moveY)
    {
        animator.SetFloat(MoveXHash, moveX);
        animator.SetFloat(MoveYHash, moveY);
        animator.SetTrigger(skill == BossSkill.Roar ? RoarHash : StompHash);
    }

    public void ShowWarning(Vector2 center, float radius)
    {
        if (IsSpawned && IsServer) _warning.Value = new Vector3(center.x, center.y, radius);
    }

    public void HideWarning()
    {
        if (IsSpawned && IsServer) _warning.Value = Vector3.zero;
    }

    /// <summary>타격 위치만 전달하며 피해는 서버의 능력에서 한 번 처리한다.</summary>
    public void PlayGroundRupture(Vector2 center, float radius)
    {
        if (!IsSpawned || !IsServer) return;
        PlayGroundRuptureRpc(center, radius);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayGroundRuptureRpc(Vector2 center, float radius)
    {
        if (groundRupture != null) groundRupture.Play(center, radius);
    }

    private void OnWarningChanged(Vector3 previous, Vector3 current) => DrawWarning(current);

    public void ShowChargeWarning(Vector2 start, Vector2 end)
    {
        if (IsSpawned && IsServer) _chargeWarning.Value = new Vector4(start.x, start.y, end.x, end.y);
    }

    public void HideChargeWarning()
    {
        if (IsSpawned && IsServer) _chargeWarning.Value = Vector4.zero;
    }

    private void OnChargeWarningChanged(Vector4 previous, Vector4 current) => DrawChargeWarning(current);

    private void DrawChargeWarning(Vector4 value)
    {
        if (chargeWarning == null) return;
        chargeWarning.enabled = value != Vector4.zero;
        if (!chargeWarning.enabled) return;
        chargeWarning.SetPosition(0, new Vector3(value.x, value.y, warningZ));
        chargeWarning.SetPosition(1, new Vector3(value.z, value.w, warningZ));
    }

    /// <summary>예고 상태가 바뀔 때만 그려서 매 프레임 원을 다시 계산하지 않는다.</summary>
    private void DrawWarning(Vector3 state)
    {
        if (warningCircle == null) return;
        warningCircle.enabled = state.z > 0f;
        if (warningFill != null)
        {
            warningFill.enabled = state.z > 0f;
            if (warningFill.enabled) UpdateFillTransform(state);
        }
        if (!warningCircle.enabled) return;
        for (int i = 0; i < CirclePoints; i++)
        {
            float angle = i * Mathf.PI * 2f / CirclePoints;
            warningCircle.SetPosition(i, new Vector3(
                state.x + Mathf.Cos(angle) * state.z,
                state.y + Mathf.Sin(angle) * state.z, warningZ));
        }
    }

    /// <summary>반경 1의 공유 원판을 피해 반경으로 확대하고 부모 프리팹 스케일을 상쇄한다.</summary>
    private void UpdateFillTransform(Vector3 state)
    {
        Transform fill = warningFill.transform;
        fill.SetPositionAndRotation(new Vector3(state.x, state.y, warningZ), Quaternion.identity);
        Vector3 parentScale = fill.parent != null ? fill.parent.lossyScale : Vector3.one;
        fill.localScale = new Vector3(
            state.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            state.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }
}
