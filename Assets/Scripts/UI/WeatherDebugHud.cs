// 기존 날씨 변경 버튼에 Day/Night·남은 시간과 디버그 Reset을 붙입니다.
// 별도 타이머를 만들지 않고 Core 공용 낮밤 source를 읽습니다.
// Reset은 현재 구간을 재시작한 뒤 기존 버튼의 날씨 변경 명령을 한 번 호출합니다.
using System;
using TeamHJD.Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.UI
{
    [DisallowMultipleComponent]
    public sealed class WeatherDebugHud : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour weatherSource;
        [SerializeField] private Button weatherButton;
        private IMatchDayNightCycle _cycle;
        private IMatchDayNightDebugControl _debug;
        private TMP_Text _timer, _phase;
        private Button _reset;
        private string _originalLabel;
        private Color _originalColor;
        private Vector2 _originalAnchorMin, _originalAnchorMax, _originalPosition;
        private int _lastSeconds = int.MinValue;

        private void Start()
        {
            if (weatherSource == null || weatherButton == null)
            {
                Debug.LogError("WeatherDebugHud에 실제 날씨 원본과 기존 변경 버튼을 연결하세요.", this);
                enabled = false;
                return;
            }
            _timer = weatherButton.GetComponentInChildren<TMP_Text>();
            if (_timer == null)
            {
                Debug.LogError("기존 날씨 버튼의 TMP 폰트/라벨이 필요합니다.", this);
                enabled = false;
                return;
            }
            _originalLabel = _timer.text;
            _originalColor = _timer.color;
            _timer.color = Color.white;
            var anchor = (RectTransform)weatherButton.transform;
            _originalAnchorMin = anchor.anchorMin;
            _originalAnchorMax = anchor.anchorMax;
            _originalPosition = anchor.anchoredPosition;
            // 기존 중앙 기준 절대 좌표는 작은 Game View에서 화면 밖으로 나가므로 좌측 하단에 고정합니다.
            anchor.anchorMin = anchor.anchorMax = Vector2.zero;
            anchor.anchoredPosition = new Vector2(anchor.rect.width / 2 + 20, 68);
            _phase = Instantiate(_timer, anchor.parent);
            _phase.name = "Weather Day Night";
            Place((RectTransform)_phase.transform, anchor, 32, 20);
            _phase.fontSize = 16;
            _phase.enableAutoSizing = false;
            _phase.raycastTarget = false;
            _phase.color = Color.white;
            _phase.text = "—";

            // 기존 sprite·색·폰트·전환을 복제하되 persistent 날씨 명령은 복제 버튼에서 제거합니다.
            _reset = Instantiate(weatherButton, anchor.parent);
            _reset.name = "Weather Timer Reset";
            Place((RectTransform)_reset.transform, anchor, -38, anchor.rect.height);
            _reset.onClick = new Button.ButtonClickedEvent();
            _reset.onClick.AddListener(ResetTimerAndWeather);
            _reset.GetComponentInChildren<TMP_Text>().text = "Reset";
            Refresh();
        }

        private void Update()
        {
            if (_timer != null) Refresh();
        }

        private void Refresh()
        {
            if (_cycle == null)
            {
                _cycle = weatherSource.GetComponent<IMatchDayNightCycle>();
                _debug = weatherSource.GetComponent<IMatchDayNightDebugControl>();
            }
            bool available = _cycle != null && !_cycle.TimeOfDayId.IsEmpty;
            string phase = available ? _cycle.Phase.ToString() : "—";
            if (_phase.text != phase) _phase.text = phase;
            double remaining = available ? _cycle.RemainingSeconds : double.NaN;
            int seconds = double.IsFinite(remaining) ? (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, remaining))) : -1;
            if (_lastSeconds != seconds)
            {
                _lastSeconds = seconds;
                _timer.text = seconds < 0 ? "--:--" : $"{seconds / 60}:{seconds % 60:00}";
            }
            _reset.interactable = _debug?.CanRestartCurrentPhase == true;
        }

        /// <summary>Host 디버그 버튼 전용입니다. 현재 낮밤은 유지하고 5분을 다시 시작하며 기존 날씨 큐를 한 번 진행합니다.</summary>
        public void ResetTimerAndWeather()
        {
            if (_debug?.TryRestartCurrentPhase() != true) return;
            weatherButton.onClick.Invoke();
            Refresh();
        }

        private static void Place(RectTransform rect, RectTransform anchor, float offsetY, float height)
        {
            rect.anchorMin = anchor.anchorMin;
            rect.anchorMax = anchor.anchorMax;
            rect.pivot = anchor.pivot;
            rect.sizeDelta = new Vector2(anchor.rect.width, height);
            rect.anchoredPosition = anchor.anchoredPosition + Vector2.up * offsetY;
        }

        private void OnDestroy()
        {
            if (_timer != null)
            {
                _timer.text = _originalLabel;
                _timer.color = _originalColor;
                var anchor = (RectTransform)weatherButton.transform;
                anchor.anchorMin = _originalAnchorMin;
                anchor.anchorMax = _originalAnchorMax;
                anchor.anchoredPosition = _originalPosition;
            }
            if (_phase != null) Destroy(_phase.gameObject);
            if (_reset != null)
            {
                _reset.onClick.RemoveListener(ResetTimerAndWeather);
                Destroy(_reset.gameObject);
            }
        }
    }
}
