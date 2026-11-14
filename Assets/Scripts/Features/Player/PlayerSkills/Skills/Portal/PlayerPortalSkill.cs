using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>소유자별 한 쌍의 포털을 설치·회수하고, 검증된 도착 지점으로만 이동합니다. 입력/UI 연결은 별도입니다.</summary>
[DisallowMultipleComponent]
public sealed partial class PlayerPortalSkill : PlayerPlacementSkillBase
{
    [SerializeField] private PlayerPortalEndpoint _prefab;
    [SerializeField, Min(.1f)] private float _interactionRange = 2.5f;
    [SerializeField, Min(.1f)] private float _minimumSeparation = 3f;
    [SerializeField, Min(.1f)] private float _travelCooldown = .75f;
    private readonly PlayerPortalEndpoint[] _endpoints = new PlayerPortalEndpoint[2];
    private double _travelReadyAt;

    public override PlayerSkillId Id => PlayerSkillId.Portal;
    public override bool HasPrefab => _prefab != null && _prefab.IsConfigured && HasPlacementPreview;
    public override int AvailableCount => (_endpoints[0] == null ? 1 : 0) + (_endpoints[1] == null ? 1 : 0);
    public override float CooldownDuration => 0;
    public override float RemainingCooldown => 0;
    public override bool IsReady => base.IsReady && CanMutate;
    public bool IsLinked => IsEndpointActive(0) && IsEndpointActive(1);
    public float TravelRemaining => (float)System.Math.Max(0, _travelReadyAt - FieldSimulationTime.Seconds(this));
    public string InteractionFailure { get; private set; } = "";
    public override string PlacementHint => IsPlacing
        ? $"포털 {(NextSlot == 0 ? "A" : "B")} · 왼쪽 클릭 설치 · 우클릭 / Esc 취소" : "";
    public PlayerPortalEndpoint GetEndpoint(int slot) => slot >= 0 && slot < 2 ? _endpoints[slot] : null;
    private int NextSlot => _endpoints[0] == null ? 0 : _endpoints[1] == null ? 1 : -1;
    private bool IsEndpointActive(int slot) => _endpoints[slot] != null && _endpoints[slot].isActiveAndEnabled
        && _endpoints[slot].gameObject.scene == gameObject.scene;

    private bool CanMutate
    {
        get
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !FieldSimulationTime.CanAdvance(this)
                || ControlState != null && !ControlState.CanAct) return false;
            if (!float.IsFinite(_interactionRange) || _interactionRange <= 0
                || !float.IsFinite(_minimumSeparation) || _minimumSeparation <= 0
                || !float.IsFinite(_travelCooldown) || _travelCooldown <= 0) return false;
            var health = GetComponent<PlayerInfo>();
            if (health != null && (!health.isActiveAndEnabled || health.IsDestroyed)) return false;
            var presence = GetComponent<PlayerMatchPresence>();
            if (presence != null && presence.TimeSource != null && (!presence.CanReport || !presence.CanSimulateField)) return false;
            var network = Unity.Netcode.NetworkManager.Singleton;
            return network == null || !network.IsListening || network.IsServer;
        }
    }
    private bool CanInteract => CanMutate && !IsPlacing && (Controller == null || Controller.CanRecover);

    protected override string GetPlacementFailure(Vector3 position)
    {
        if (!CanMutate) return "지금은 포털을 설치할 수 없습니다.";
        if (NextSlot < 0) return "포털은 한 쌍만 유지할 수 있습니다. 먼저 회수해 주세요.";
        if (_prefab == null || !_prefab.IsConfigured) return "포털 본체 설정을 확인해 주세요.";
        foreach (var endpoint in _endpoints)
            if (endpoint != null && ((Vector2)(endpoint.transform.position - position)).sqrMagnitude < _minimumSeparation * _minimumSeparation)
                return "다른 포털과 너무 가깝습니다.";
        string failure = PlayerSkillDescriptions.TurretPlacementFailure(EvaluateFootprint(_prefab.FootprintAt(position)));
        return failure.Length > 0 ? failure : LandingFailure(position);
    }

    protected override bool TryPlace(Vector3 position, out bool complete)
    {
        complete = true;
        if (!CanMutate || NextSlot < 0) return false;
        int slot = NextSlot;
        _endpoints[slot] = SpawnEndpoint(slot, position);
        _endpoints[slot].GetComponent<PlayerPlacementPresentation>()?.Play();
        return true;
    }

    private PlayerPortalEndpoint SpawnEndpoint(int slot, Vector3 position)
    {
        var endpoint = Instantiate(_prefab, position, Quaternion.identity);
        MoveToOwnerScene(endpoint.gameObject);
        endpoint.Initialize(slot, transform);
        return endpoint;
    }

    /// <summary>R 이동·F 회수/안내 UI가 조회할 소유 포털입니다. 벽 너머의 포털은 후보가 되지 않습니다.</summary>
    public PlayerPortalEndpoint NearestInteractable
    {
        get
        {
            if (!CanInteract) return null;
            Physics2D.SyncTransforms();
            Vector2 feet = Feet.center;
            float nearestDistance = _interactionRange * _interactionRange;
            PlayerPortalEndpoint nearest = null;
            for (int slot = 0; slot < 2; slot++)
            {
                if (!IsEndpointActive(slot)) continue;
                var endpoint = _endpoints[slot];
                float distance = ((Vector2)endpoint.FootprintAt(endpoint.transform.position).ClosestPoint(feet) - feet).sqrMagnitude;
                if (distance > nearestDistance || HasInteractionObstacle(feet, endpoint)) continue;
                nearest = endpoint; nearestDistance = distance;
            }
            return nearest;
        }
    }

    /// <summary>명시적으로 누른 이동 요청만 실행합니다. 도착 후 Trigger에 의해 자동 왕복하지 않습니다.</summary>
    public bool TryTravel()
    {
        InteractionFailure = "";
        var source = NearestInteractable;
        if (source == null) return Reject("가까운 포털이 없거나 지금은 사용할 수 없습니다.");
        if (!IsLinked) return Reject("포털 두 개를 모두 설치해 주세요.");
        if (TravelRemaining > 0) return Reject("잠시 후 포털을 다시 사용할 수 있습니다.");
        Vector3 destinationFeet = _endpoints[1 - source.SlotIndex].transform.position;
        InteractionFailure = LandingFailure(destinationFeet);
        if (InteractionFailure.Length > 0) return false;
        var movement = GetComponent<PlayerMovement>();
        var body = GetComponent<Rigidbody2D>();
        if (movement == null || body == null || !body.simulated || !movement.CanAcceptInput)
            return Reject("플레이어 이동 설정을 확인해 주세요.");
        // 스프라이트 pivot이 아닌 실제 발밑 중심을 포털 중심에 맞춥니다. 기존 Z 정렬은 유지합니다.
        Vector3 position = transform.position + destinationFeet - Feet.center;
        position.z = transform.position.z;
        movement.StopImmediately();
        body.position = position;
        transform.position = position;
        Physics2D.SyncTransforms();
        _travelReadyAt = FieldSimulationTime.Seconds(this) + Mathf.Max(.1f, _travelCooldown);
        return true;
    }

    /// <summary>가까운 자기 포털 하나만 회수합니다. 나머지는 유지되며 빈 A/B 슬롯에 재설치합니다.</summary>
    public bool TryRecoverNearest()
    {
        var endpoint = NearestInteractable;
        if (endpoint == null) return false;
        _endpoints[endpoint.SlotIndex] = null;
        endpoint.gameObject.SetActive(false);
        PlayerFieldDevice.Dispose(endpoint.gameObject);
        InteractionFailure = "";
        return true;
    }

    private bool Reject(string reason) { InteractionFailure = reason; return false; }
    private Bounds Feet
    {
        get
        {
            var directional = GetComponent<PlayerDirectionalColliders>();
            if (directional != null && directional.isActiveAndEnabled) return directional.GroundBounds;
            var collider = GetComponent<Collider2D>();
            return collider != null && collider.enabled && !collider.isTrigger ? collider.bounds
                : new Bounds(transform.position, Vector3.one * .5f);
        }
    }

    private string LandingFailure(Vector3 position)
    {
        Bounds footprint = Feet; footprint.center = new Vector3(position.x, position.y, footprint.center.z);
        footprint.Expand(new Vector3(.1f, .1f, 0));
        if (!PlayerTurretPlacementRules.IsOnGround(PlacementGround, footprint)) return "도착 지점에 충분한 육지가 없습니다.";
        Physics2D.SyncTransforms();
        foreach (var hit in Physics2D.OverlapBoxAll(footprint.center, footprint.size, 0))
            if (BlocksPlayer(hit)) return "도착 지점이 다른 캐릭터나 장애물에 막혀 있습니다.";
        return "";
    }

    private bool HasInteractionObstacle(Vector2 from, PlayerPortalEndpoint endpoint)
    {
        Vector2 delta = (Vector2)endpoint.transform.position - from;
        if (delta.sqrMagnitude < .000001f) return false;
        foreach (var hit in Physics2D.RaycastAll(from, delta.normalized, delta.magnitude))
            // 상호작용하는 포털 자신의 받침은 접근을 막아도 R/F 대상 선택까지 가리지 않습니다.
            // 다른 포털·벽은 계속 검사하고 도착 판정에서는 자기 받침도 그대로 검사합니다.
            if (hit.collider != null && !hit.collider.transform.IsChildOf(endpoint.transform) && BlocksPlayer(hit.collider)) return true;
        return false;
    }

    private bool BlocksPlayer(Collider2D hit)
    {
        if (hit == null || !hit.enabled || hit.isTrigger || hit.gameObject.scene != gameObject.scene
            || hit.transform.IsChildOf(transform)) return false;
        if (hit.GetComponentInParent<EnemyHealth>() != null || hit.GetComponentInParent<PlayerInfo>() != null
            || hit.GetComponentInParent<PlayerTurret>() != null
            || hit.GetComponentInParent<TeamHJD.Game.FieldTurrets.FieldTurretBase>() != null) return true;
        int feetLayer = LayerMask.NameToLayer("PlayerFeetBox");
        if (feetLayer < 0) feetLayer = gameObject.layer;
        int mask = 1 << feetLayer;
        return (hit.excludeLayers.value & mask) == 0 && ((hit.includeLayers.value & mask) != 0
            || !Physics2D.GetIgnoreLayerCollision(feetLayer, hit.gameObject.layer));
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        for (int slot = 0; slot < 2; slot++)
            if (_endpoints[slot] != null) PlayerFieldDevice.Dispose(_endpoints[slot].gameObject);
    }
}
