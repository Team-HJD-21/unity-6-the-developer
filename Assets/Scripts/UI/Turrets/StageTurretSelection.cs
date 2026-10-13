using System.Collections.Generic;
using TeamHJD.Game.Turrets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>씬 소속 터렛을 선택하며 선택한 터렛에만 사거리를 표시합니다.</summary>
internal sealed class StageTurretSelection
{
    private readonly Scene _scene;
    private readonly List<RaycastResult> _uiHits = new();
    private TurretBase _selected;
    public int SelectedId { get; private set; }

    public StageTurretSelection(Scene scene) => _scene = scene;

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

        Clear();
        _selected = clickedTurret;
        SelectedId = clickedTurret != null ? clickedTurret.InstanceId : 0;
        if (_selected != null) _selected.ShowRange = true;
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
            if (candidate == null || candidate.gameObject.scene != _scene || !candidate.isActiveAndEnabled)
                continue;
            if (!TurretInstanceRegistry.TryGet(candidate.InstanceId, out var registered) || registered != candidate)
                continue;

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
            current.gameObject.scene != _scene || !current.isActiveAndEnabled)
        {
            Clear();
            return null;
        }

        if (_selected != current)
        {
            if (_selected != null) _selected.ShowRange = false;
            _selected = current;
        }

        _selected.ShowRange = true;
        return _selected;
    }

    public void Clear()
    {
        if (_selected != null) _selected.ShowRange = false;
        _selected = null;
        SelectedId = 0;
    }
}
