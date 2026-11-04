using System;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬 소속 터렛을 선택하며 선택한 터렛에만 사거리를 표시합니다.</summary>
internal sealed class StageTurretSelection
{
    private readonly Scene _scene;
    private readonly Func<PlayerInfo> _getPlayer;
    private readonly Func<float> _getInteractionDistance;
    private FieldTurretBase _selected;
    public int SelectedId { get; private set; }

    public StageTurretSelection(Scene scene, Func<PlayerInfo> getPlayer,
        Func<float> getInteractionDistance)
    {
        _scene = scene;
        _getPlayer = getPlayer;
        _getInteractionDistance = getInteractionDistance;
    }

    private bool IsWithinReach(FieldTurretBase turret)
    {
        PlayerInfo player = _getPlayer();
        float distance = _getInteractionDistance();
        return player != null && player.isActiveAndEnabled && distance > 0f &&
            ((Vector2)player.transform.position - (Vector2)turret.transform.position).sqrMagnitude
            <= distance * distance;
    }

    public bool CanSelect(FieldTurretBase turret) => turret != null &&
        turret.gameObject.scene == _scene && turret.isActiveAndEnabled &&
        FieldTurretInstanceRegistry.TryGet(turret.InstanceId, out var registered) && registered == turret;

    public bool CanInteractWith(FieldTurretBase turret) => CanSelect(turret) && IsWithinReach(turret);

    public void ProcessInput(FieldTurretController controller)
    {
        if (GameInput.WasPressedThisFrame(GameKey.Escape))
        {
            Clear();
            return;
        }

        if (controller == null || !GameInput.PrimaryPointerWasPressedThisFrame ||
            GameInput.PrimaryPointerWasConsumed || GameInput.PrimaryPointerReservedForPlacement || GameInput.IsPointerOverUi())
            return;

        FieldTurretBase clickedTurret = FindClickedTurret();
        // 이미 선택한 포탑이나 먼 포탑도 선택 클릭이므로 공격으로 넘기지 않습니다.
        if (clickedTurret != null && !GameInput.TryConsumePrimaryPointerPress()) return;
        if (_selected == clickedTurret) return;

        if (clickedTurret == null) Clear();
        else TrySelect(clickedTurret);
    }

    public bool TrySelect(FieldTurretBase turret)
    {
        if (!CanSelect(turret)) return false;
        Clear(); _selected = turret; SelectedId = turret.InstanceId;
        // Client의 실제 사거리 시각화는 D 표현 복제 연결 전까지 로컬 초기값으로 그리지 않습니다.
        _selected.ShowRange = _selected.HasStateAuthority; return true;
    }

    private FieldTurretBase FindClickedTurret()
    {
        Camera camera = Camera.main;
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(GameInput.PointerPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return null;

        Vector2 point = ray.GetPoint(distance);
        FieldTurretBase nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
        {
            FieldTurretBase candidate = hit.GetComponentInParent<FieldTurretBase>();
            if (!CanSelect(candidate)) continue;

            float distanceSquared = ((Vector2)candidate.transform.position - point).sqrMagnitude;
            if (distanceSquared >= nearestDistance) continue;

            nearest = candidate;
            nearestDistance = distanceSquared;
        }

        return nearest;
    }

    public FieldTurretBase Resolve()
    {
        if (SelectedId == 0) return null;

        if (!FieldTurretInstanceRegistry.TryGet(SelectedId, out var current) || current == null ||
            !CanSelect(current))
        {
            Clear();
            return null;
        }

        if (_selected != current)
        {
            if (_selected != null) _selected.ShowRange = false;
            _selected = current;
        }

        _selected.ShowRange = _selected.HasStateAuthority;
        return _selected;
    }

    public void Clear()
    {
        if (_selected != null) _selected.ShowRange = false;
        _selected = null;
        SelectedId = 0;
    }
}
