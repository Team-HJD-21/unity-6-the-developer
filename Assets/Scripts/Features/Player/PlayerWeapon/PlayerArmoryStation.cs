using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>우주선 WeaponChoice에서 로컬 방문자만 거리·생존·입력 상태를 검사해 무기고를 엽니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-310)]
public sealed class PlayerArmoryStation : MonoBehaviour
{
    [SerializeField] private PlayerArmoryView _viewPrefab;
    [SerializeField, Min(.1f)] private float _interactionRange = 3f;
    private PlayerArmoryView _view;
    private PlayerArmory _owner;
    private GameObject _ownedEvents;
    public PlayerArmoryView View => _view;
    public bool IsInRange(PlayerArmory player) => isActiveAndEnabled && player != null &&
        Vector2.SqrMagnitude((Vector2)player.transform.position - (Vector2)transform.position) <= _interactionRange * _interactionRange;
    private void Update()
    {
        if (_view == null && _viewPrefab != null) _view = Instantiate(_viewPrefab, transform);
        if (_view == null) return;
        if (_owner != null)
        {
            if (!_owner.IsOpen || !_owner.IsAlive || !_owner.IsInSpaceship || !IsInRange(_owner) || Time.timeScale <= 0)
            { Close(); return; }
            if (UIEscapeInput.TryConsume() || GameInput.WasPressedThisFrame(GameKey.F)) { Close(); return; }
            _view.Refresh(); return;
        }
        PlayerArmory nearest = null; float distance = float.PositiveInfinity;
        foreach (var actor in PlayerArmory.LocalActors)
        {
            if (!IsInRange(actor) || !actor.CanOpen) continue;
            float next = (actor.transform.position - transform.position).sqrMagnitude;
            if (next < distance) { nearest = actor; distance = next; }
        }
        _view.ShowPrompt(nearest != null);
        if (nearest == null || !GameInput.WasPressedThisFrame(GameKey.F) || IsEditingText()) return;
        TryOpen(nearest);
    }
    private static bool IsEditingText() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
        EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;
    public bool TryOpen(PlayerArmory owner)
    {
        if (owner == null || _view == null || !_view.IsConfigured || _owner != null || !owner.TryOpen(this)) return false;
        // Startup's menu EventSystem is unloaded before the first field visit.
        // Reuse a scene system when present; otherwise own one only while this UI is open.
        if (EventSystem.current == null)
        {
            _ownedEvents = new GameObject("Armory EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            _ownedEvents.transform.SetParent(transform, false);
        }
        _owner = owner; _view.Bind(owner, this); return true;
    }
    public void Close()
    {
        if (_owner != null) _owner.Close(); _owner = null;
        if (_view != null) _view.Hide();
        if (_ownedEvents != null)
        {
            _ownedEvents.SetActive(false);
            Destroy(_ownedEvents);
            _ownedEvents = null;
        }
    }
    private void OnDisable() => Close();
    private void OnDestroy() { if (_view != null) Destroy(_view.gameObject); }
}
