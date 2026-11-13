// 개인 메뉴가 열릴 때만 우측 디버그를 표시합니다. Auto는 해당 메뉴만 닫은 뒤 기존 HUD 명령 Port로 요청합니다.
// 다른 생존/전환 잠금과 Host 검증을 제거하지 않으며, 전장의 pause 정책을 바꾸지 않습니다.
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Application;
using TeamHJD.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatHudDebugPanel : MonoBehaviour
{
    public CombatHudStyle Style;
    public InitialStagePlayerHUD Stage;
    public CanvasGroup Panel, Toast;
    public RectTransform PanelRect;
    public Button Reset, Advance, Auto;
    public TMP_Text ToastLabel;
    private AppRoot _app;
    private WeatherDebugHud _weather;
    private readonly CombatHudTransition _panel = new();
    private float _nextSearch;
    private Coroutine _autoRequest;

    private void OnEnable()
    {
        Reset.onClick.AddListener(ResetWeather); Advance.onClick.AddListener(AdvanceWeather); Auto.onClick.AddListener(Activate);
        Stage.AutoActivationFeedback += ShowToast;
        // 기존 제작 참조는 보존하되 별도 토스트는 더 이상 출력하지 않습니다.
        if (ToastLabel != null) ToastLabel.text = "";
        if (Toast != null) { Toast.alpha = 0; Toast.blocksRaycasts = Toast.interactable = false; }
    }
    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextSearch && (_app == null || _weather == null))
        {
            _nextSearch = Time.unscaledTime + .5f;
            if (_app == null) _app = FindFirstObjectByType<AppRoot>();
            if (_weather == null)
                foreach (var source in FindObjectsByType<WeatherDebugHud>(FindObjectsSortMode.None))
                    if (source.gameObject.scene == gameObject.scene) { _weather = source; break; }
        }
        if (_weather != null) _weather.SuppressLegacyControls();
        bool appReady = _app != null && _app.IsInitialized;
        bool menu = Stage.HasFieldActor && appReady && _app.Screens.Top?.Kind == ScreenKind.Menu;
        bool covered = !appReady || (_app.Screens.Top != null && _app.Screens.Top.Kind != ScreenKind.Menu);
        if (!Stage.HasFieldActor || covered) _panel.Snap(0);
        _panel.Set(menu ? 1 : 0, Style.SlideSeconds);
        float amount = _panel.Tick(Time.unscaledDeltaTime);
        Panel.alpha = amount; Panel.blocksRaycasts = Panel.interactable = menu;
        float outside = ((RectTransform)PanelRect.parent).rect.width * .5f + PanelRect.rect.width * .5f;
        PanelRect.anchoredPosition = new Vector2(Mathf.Lerp(outside, 492, amount), 0);
        Reset.interactable = Advance.interactable = _weather != null && _weather.CanChangePhase;
        Auto.interactable = menu && Stage.isActiveAndEnabled;
        if (Toast != null) Toast.alpha = 0;
    }
    private void ResetWeather()
    {
        if (_weather == null || !_weather.CanChangePhase) return;
        _weather.ResetTimerAndWeather(); GameAlerts.Show("구간 시간과 날씨를 초기화했습니다.", AlertTone.Success);
    }
    private void AdvanceWeather()
    {
        if (_weather == null || !_weather.CanChangePhase) return;
        _weather.AdvanceDayNight(); GameAlerts.Show("낮밤 전환을 요청했습니다.", AlertTone.Info);
    }
    private void Activate()
    {
        var menu = _app != null && _app.IsInitialized ? _app.Screens.Top : null;
        if (menu == null || menu.Kind != ScreenKind.Menu || !_app.Screens.Close(menu.Id)) return;
        if (_autoRequest != null) StopCoroutine(_autoRequest);
        _autoRequest = StartCoroutine(RequestAfterMenuClose());
    }
    private System.Collections.IEnumerator RequestAfterMenuClose()
    {
        // 입력 lease 해제의 로컬 actor 투영도 한 프레임 진행한 뒤 기존 검사를 그대로 통과시킵니다.
        yield return null;
        _autoRequest = null;
        if (Stage != null && Stage.HasFieldActor && _app != null && _app.IsInitialized && _app.Screens.Top == null) Stage.TryRequestAutoActivation();
    }
    private void ShowToast(string text)
    {
        // CU View가 이미 송출하는 최종 응답/실패와 대기 안내를 여기서 다시 복제하지 않습니다.
        if (text == "자동 활성화가 시작되었습니다." || text == "자동 활성화 요청이 거절되었습니다." ||
            text == "요청을 보낼 수 없거나 이미 처리 중입니다." || text == "자동 활성화를 요청했습니다.") return;
        GameAlerts.Show(text, key: "cu/auto/preflight");
    }
    private void OnDisable()
    {
        if (_autoRequest != null) { StopCoroutine(_autoRequest); _autoRequest = null; }
        if (Reset != null) Reset.onClick.RemoveListener(ResetWeather);
        if (Advance != null) Advance.onClick.RemoveListener(AdvanceWeather);
        if (Auto != null) Auto.onClick.RemoveListener(Activate);
        if (Stage != null) Stage.AutoActivationFeedback -= ShowToast;
        _panel.Snap(0);
        if (Panel != null) { Panel.alpha = 0; Panel.blocksRaycasts = false; }
        if (Toast != null) Toast.alpha = 0;
    }
}
