using System;
using System.Collections.Generic;
using TeamHJD.Game.Turrets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>씬 소속 터렛을 선택하며 선택한 터렛에만 사거리를 표시합니다.</summary>
internal sealed class StageTurretSelection
{
    private readonly Scene _scene;
    private readonly Func<PlayerInfo> _getPlayer;
    private readonly Func<float> _getInteractionDistance;
    private readonly List<RaycastResult> _uiHits = new();
    private TurretBase _selected;
    public int SelectedId { get; private set; }

    public StageTurretSelection(Scene scene, Func<PlayerInfo> getPlayer,
        Func<float> getInteractionDistance)
    {
        _scene = scene;
        _getPlayer = getPlayer;
        _getInteractionDistance = getInteractionDistance;
    }

    private bool IsWithinReach(TurretBase turret)
    {
        PlayerInfo player = _getPlayer();
        float distance = _getInteractionDistance();
        return player != null && player.isActiveAndEnabled && distance > 0f &&
            ((Vector2)player.transform.position - (Vector2)turret.transform.position).sqrMagnitude
            <= distance * distance;
    }

    public bool CanSelect(TurretBase turret) => turret != null &&
        turret.gameObject.scene == _scene && turret.isActiveAndEnabled && IsWithinReach(turret) &&
        TurretInstanceRegistry.TryGet(turret.InstanceId, out var registered) && registered == turret;

    public void ProcessInput(TurretController controller)
    {
        if (GameInput.WasPressedThisFrame(GameKey.Escape))
        {
            Clear();
            return;
        }

        if (controller == null || !GameInput.PrimaryPointerWasPressedThisFrame || IsPointerOverUi())
            return;

        TurretBase clickedTurret = FindClickedTurret();
        if (_selected == clickedTurret) return;

        if (clickedTurret == null) Clear();
        else TrySelect(clickedTurret);
    }

    public bool TrySelect(TurretBase turret)
    {
        if (!CanSelect(turret)) return false;
        Clear(); _selected = turret; SelectedId = turret.InstanceId;
        // Client의 실제 사거리 시각화는 D 표현 복제 연결 전까지 로컬 초기값으로 그리지 않습니다.
        _selected.ShowRange = _selected.HasStateAuthority; return true;
    }

    private bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        _uiHits.Clear();
        var pointer = new PointerEventData(eventSystem) { position = GameInput.PointerPosition };
        eventSystem.RaycastAll(pointer, _uiHits);
        return _uiHits.Count > 0;
    }

    private TurretBase FindClickedTurret()
    {
        Camera camera = Camera.main;
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(GameInput.PointerPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return null;

        Vector2 point = ray.GetPoint(distance);
        TurretBase nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
        {
            TurretBase candidate = hit.GetComponentInParent<TurretBase>();
            if (!CanSelect(candidate)) continue;

            float distanceSquared = ((Vector2)candidate.transform.position - point).sqrMagnitude;
            if (distanceSquared >= nearestDistance) continue;

            nearest = candidate;
            nearestDistance = distanceSquared;
        }

        return nearest;
    }

    public TurretBase Resolve()
    {
        if (SelectedId == 0) return null;

        if (!TurretInstanceRegistry.TryGet(SelectedId, out var current) || current == null ||
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
