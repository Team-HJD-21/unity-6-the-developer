// 설치형 스킬의 공통 배치/입력 흐름과 생성 자식의 Match 소유권을 연결합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>원형 범위 안의 커서 위치 미리보기·클릭 확정·취소를 설치형 스킬에 제공합니다.</summary>
public abstract class PlayerPlacementSkillBase : PlayerSkillBase
{
    [Header("Placement")]
    [SerializeField] private PlayerSkillPlacementPreview _previewPrefab;
    [SerializeField, Min(.1f)] private float _placementRadius = 12f;
    [SerializeField] private Tilemap _placementGround;
    private PlayerSkillPlacementPreview _preview;
    private PlayerInputHandler _placementInput;
    private float _nextCheck;
    private int _startFrame;
    public bool IsPlacing => _preview != null;
    public virtual float PlacementRadius => Mathf.Max(.1f, _placementRadius);
    public virtual Vector3 PlacementCenter => transform.position;
    protected bool HasPlacementPreview => _previewPrefab != null && _previewPrefab.IsConfigured;
    protected virtual Vector3? PlacementAnchor => null;
    public virtual string PlacementHint => IsPlacing ? "왼쪽 클릭으로 설치 · 오른쪽 클릭 / Esc / 같은 스킬 키로 취소" : "";
    public string PlacementFailure { get; protected set; } = "";

    protected override void Awake()
    {
        base.Awake();
        _placementInput = GetComponent<PlayerInputHandler>();
    }

    protected sealed override bool TryActivate()
    {
        GameInput.ReservePrimaryPointerForPlacement();
        if (IsPlacing) { CancelPlacement(); return true; }
        CreatePlacementPreview();
        OnPlacementStarted();
        return true;
    }

    private void CreatePlacementPreview()
    {
        _preview = Instantiate(_previewPrefab, transform.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_preview.gameObject, gameObject.scene);
        _preview.HideCandidate();
        _preview.RefreshRange(PlacementCenter, PlacementRadius);
        _startFrame = Time.frameCount;
        _nextCheck = 0;
    }

    /// <summary>스킬 writer가 저장된 설치 의도를 복원합니다. 입력·수량 소비·배치 확정은 발생시키지 않습니다.</summary>
    protected void RestorePlacementState(bool placing)
    {
        CancelPlacement();
        if (placing) CreatePlacementPreview();
    }

    // UI 소비가 끝난 뒤 확정합니다. 설치 시작에 사용한 클릭은 다음 프레임까지 재사용하지 않습니다.
    protected virtual void LateUpdate()
    {
        if (!IsPlacing) return;
        GameInput.ReservePrimaryPointerForPlacement();
        if (!CanContinuePlacement() || GameInput.SecondaryPointerWasPressedThisFrame || GameInput.WasPressedThisFrame(GameKey.Escape))
        { CancelPlacement(); return; }
        _preview.RefreshRange(PlacementCenter, PlacementRadius);
        if (_placementInput == null || !_placementInput.HasAimTarget) { _preview.HideCandidate(); return; }
        Vector3 position = GetPlacementPosition(_placementInput.MouseWorldPosition);
        if (Time.unscaledTime >= _nextCheck)
        {
            EvaluateCandidate(position);
            _nextCheck = Time.unscaledTime + .05f;
        }
        _preview.Refresh(position, PlacementFailure.Length == 0, PlacementAnchor);
        _preview.RefreshRange(PlacementCenter, PlacementRadius);
        if (Time.frameCount > _startFrame && !GameInput.IsPointerOverUi() && GameInput.TryConsumePrimaryPointerPress())
            TryConfirmPlacement(position);
    }

    private bool CanContinuePlacement() => isActiveAndEnabled && Time.timeScale > 0 && IsReady
        && (ControlState == null || ControlState.CanAct)
        && (Controller == null || Controller.CanUse && Controller.Loadout.Contains(Id));

    /// <summary>XY는 커서 그대로 사용하며 정렬용 Z만 플레이어와 맞춥니다. 범위 밖도 이동시키거나 보정하지 않습니다.</summary>
    public Vector3 GetPlacementPosition(Vector2 pointer) => new(pointer.x, pointer.y, transform.position.z);

    public bool TryConfirmPlacement(Vector3 pointer)
    {
        if (!IsPlacing || !CanContinuePlacement() || !PlayerSkillPlacementUtility.IsFinite(pointer)) return false;
        Vector3 position = GetPlacementPosition(pointer);
        // 프리뷰 갱신 주기와 관계없이 확정 순간 이동한 몬스터·장애물까지 다시 검사합니다.
        EvaluateCandidate(position);
        _preview.Refresh(position, PlacementFailure.Length == 0, PlacementAnchor);
        _preview.RefreshRange(PlacementCenter, PlacementRadius);
        if (PlacementFailure.Length > 0 || !TryPlace(position, out bool complete)) return false;
        if (complete) CancelPlacement();
        else
        {
            _nextCheck = 0;
            _preview.Refresh(position, true, PlacementAnchor);
            _preview.RefreshRange(PlacementCenter, PlacementRadius);
        }
        return true;
    }

    protected PlayerTurretPlacementResult EvaluateFootprint(Bounds footprint)
    {
        return PlayerTurretPlacementRules.EvaluateFootprint(footprint, gameObject, PlacementGround);
    }

    protected Tilemap PlacementGround
    {
        get
        {
            if (_placementGround == null) _placementGround = PlayerTurretPlacementRules.FindGround(gameObject.scene);
            return _placementGround;
        }
    }

    private void EvaluateCandidate(Vector3 position)
    {
        if (!PlayerSkillPlacementUtility.IsFinite(position))
        { PlacementFailure = "유효하지 않은 설치 위치입니다."; return; }
        if (((Vector2)(position - PlacementCenter)).sqrMagnitude > PlacementRadius * PlacementRadius)
        { PlacementFailure = "설치 가능 범위를 벗어났습니다. 원 안의 위치를 선택해 주세요."; return; }
        Physics2D.SyncTransforms();
        PlacementFailure = GetPlacementFailure(position);
    }

    protected abstract string GetPlacementFailure(Vector3 position);
    protected abstract bool TryPlace(Vector3 position, out bool complete);
    protected virtual void OnPlacementStarted() { }
    protected virtual void OnPlacementCancelled() { }

    public void CancelPlacement()
    {
        if (_preview != null)
        {
            _preview.SetVisible(false);
            PlayerFieldDevice.Dispose(_preview.gameObject);
            _preview = null;
        }
        PlacementFailure = "";
        OnPlacementCancelled();
    }

    protected void MoveToOwnerScene(GameObject placed) => FieldSimulationTime.BindSpawned(placed, this);
    protected virtual void OnDisable() => CancelPlacement();
    protected virtual void OnDestroy() => CancelPlacement();
}
