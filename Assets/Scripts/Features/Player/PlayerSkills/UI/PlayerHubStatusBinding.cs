using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>시작 우주선의 명시적인 로컬 actor에 상태/스킬 창만 연결합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerHubStatusBinding : MonoBehaviour, IHubPlayerBinding
{
    [SerializeField] private GameObject _statusHudPrefab;
    private GameObject _hud;
    private GameObject _ownedEvents;
    private bool _bound;
    public PlayerStatusPanel Panel => _hud != null ? _hud.GetComponent<PlayerStatusPanel>() : null;
    public void BindHub(IInputBlocker input)
    {
        _bound = true;
        EnsureHud();
    }
    private void EnsureHud()
    {
        if (!_bound || _hud != null || !isActiveAndEnabled || _statusHudPrefab == null) return;
        _hud = Instantiate(_statusHudPrefab);
        SceneManager.MoveGameObjectToScene(_hud, gameObject.scene);
        _hud.GetComponent<StagePlayerVitalsView>().BindPlayer(GetComponent<PlayerInfo>());
        EnsureUiInput();
    }
    // 이전 메뉴 Scene의 EventSystem이 허브 바인딩 뒤 사라지는 경우도 처리합니다.
    private void Update() => EnsureUiInput();
    private void EnsureUiInput()
    {
        if (!_bound || _hud == null || !_hud.activeInHierarchy || !isActiveAndEnabled ||
            _ownedEvents != null || EventSystem.current != null) return;
        _ownedEvents = new GameObject("Hub Player UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        _ownedEvents.transform.SetParent(_hud.transform, false);
    }
    public void UnbindHub()
    {
        _bound = false;
        RemoveHud();
    }
    private void RemoveHud()
    {
        if (_ownedEvents != null)
        {
            _ownedEvents.SetActive(false);
            Destroy(_ownedEvents); _ownedEvents = null;
        }
        if (_hud == null) return;
        _hud.GetComponent<PlayerStatusPanel>().Close();
        _hud.SetActive(false);
        Destroy(_hud); _hud = null;
    }
    private void OnEnable() => EnsureHud();
    private void OnDisable() => RemoveHud();
    private void OnDestroy() => RemoveHud();
}
