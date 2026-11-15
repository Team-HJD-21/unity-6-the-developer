using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>명시적으로 바인딩한 로컬 플레이어와 같은 Scene의 설치물에만 체력 HUD를 띄웁니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerInstallationHealthHud : MonoBehaviour, ILocalInputConsumer
{
    [SerializeField] private PlayerInstallationHealthView _viewPrefab;
    private PlayerInstallationHealthView _view;
    private PlayerControlState _controls;
    private PlayerInfo _health;
    private IInputBlocker _input;
    public PlayerInstallationHealthView View => _view;
    private void Awake() { _controls = GetComponent<PlayerControlState>(); _health = GetComponent<PlayerInfo>(); }
    public void BindInputBlocker(IInputBlocker input) { _input = input; EnsureView(); }
    private void EnsureView()
    {
        if (_input == null || !isActiveAndEnabled || _view != null || _viewPrefab == null) return;
        _view = Instantiate(_viewPrefab);
        SceneManager.MoveGameObjectToScene(_view.gameObject, gameObject.scene);
    }
    private void OnEnable() => EnsureView();
    private void OnDisable() => RemoveView();
    private void OnDestroy() => RemoveView();
    private void RemoveView()
    {
        if (_view == null) return;
        _view.Hide(); Destroy(_view.gameObject); _view = null;
    }
    private void LateUpdate()
    {
        if (_view == null) return;
        var output = CameraManager.Current != null ? CameraManager.Current.OutputCamera : null;
        var camera = output != null && output.isActiveAndEnabled ? output : Camera.main;
        if (camera == null) { _view?.Hide(); return; }
        var plane = new Plane(Vector3.forward, transform.position);
        var ray = camera.ScreenPointToRay(GameInput.PointerPosition);
        if (!plane.Raycast(ray, out float distance)) { _view?.Hide(); return; }
        Refresh(camera, ray.GetPoint(distance), GameInput.IsPointerOverUi());
    }
    public void Refresh(Camera camera, Vector2 pointer, bool overUi)
    {
        if (_view == null) return;
        if (!isActiveAndEnabled || _input == null || _input.IsBlocked || overUi || camera == null ||
            !camera.isActiveAndEnabled || _health == null || _health.IsDestroyed ||
            (_controls != null && (_controls.IsBlocked || _controls.IsInSpaceship))) { _view.Hide(); return; }
        PlayerInstallationHealthTarget selected = null;
        Vector3 anchor = default;
        float nearest = float.PositiveInfinity;
        foreach (var target in PlayerInstallationHealthTarget.ActiveInstances)
        {
            if (target == null || target.gameObject.scene != gameObject.scene || !target.TryHover(pointer, out var candidate)) continue;
            float distance = ((Vector2)candidate - pointer).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance; selected = target; anchor = candidate;
        }
        if (selected == null) _view.Hide();
        else _view.Show(camera, anchor, selected.Health, selected.MaxHealth);
    }
}
