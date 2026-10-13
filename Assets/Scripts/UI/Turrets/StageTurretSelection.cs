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
        if (GameInput.WasPressedThisFrame(GameKey.Escape)) { Clear(); return; }
        if (!GameInput.PrimaryPointerWasPressedThisFrame || Time.timeScale <= 0) return;
        if (EventSystem.current != null)
        {
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = GameInput.PointerPosition }, _uiHits);
            if (_uiHits.Count > 0) return;
        }
        Camera camera = Camera.main;
        if (camera == null || controller == null) return;
        Ray ray = camera.ScreenPointToRay(GameInput.PointerPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return;
        Vector2 point = ray.GetPoint(distance);
        TurretBase nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var hit in Physics2D.OverlapPointAll(point))
        {
            var turret = hit.GetComponentInParent<TurretBase>();
            if (turret == null || turret.gameObject.scene != _scene || !turret.isActiveAndEnabled ||
                !TurretInstanceRegistry.TryGet(turret.InstanceId, out var registered) || registered != turret) continue;
            // Registry is global; only this controller's turrets belong in this panel.
            if (TurretController.FindForScene(turret.gameObject.scene) != controller) continue;
            float candidateDistance = ((Vector2)turret.transform.position - point).sqrMagnitude;
            if (candidateDistance < nearestDistance) { nearest = turret; nearestDistance = candidateDistance; }
        }
        if (_selected == nearest) return;
        Clear();
        _selected = nearest;
        SelectedId = nearest != null ? nearest.InstanceId : 0;
        if (_selected != null) _selected.ShowRange = true;
    }

    public TurretBase Resolve()
    {
        if (SelectedId == 0) return null;
        if (!TurretInstanceRegistry.TryGet(SelectedId, out var current) || current == null || current.gameObject.scene != _scene)
        { Clear(); return null; }
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
