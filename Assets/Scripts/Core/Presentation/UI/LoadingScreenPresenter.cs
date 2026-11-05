// 전체 검은 배경·임시 로딩 안내·하단 진행 바를 표시합니다.
// 실제 Scene 진행률이 없으면 이동하는 막대로 대기 상태를 알리며 퍼센트를 만들어내지 않습니다.
// 입력 잠금·전환 수명은 ScreenManager/App 흐름이 관리합니다.
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    public sealed class LoadingScreenPresenter : IScreenPresenter
    {
        private readonly GameObject _overlay;
        private readonly RectTransform _fill;
        private readonly Func<float> _progress;
        private readonly Action _released;
        private bool _disposed;
        public LoadingScreenPresenter(Transform parent, Func<float> progress, Action released, Font font = null)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _released = released ?? throw new ArgumentNullException(nameof(released));
            _overlay = new GameObject("Screen:Loading", typeof(RectTransform), typeof(Image));
            _overlay.transform.SetParent(parent, false);
            Stretch((RectTransform)_overlay.transform);
            _overlay.GetComponent<Image>().color = Color.black;
            var label = new GameObject("Loading Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(_overlay.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(.1f, .06f);
            labelRect.anchorMax = new Vector2(.9f, .06f);
            labelRect.pivot = new Vector2(.5f, 0);
            labelRect.anchoredPosition = new Vector2(0, 24);
            labelRect.sizeDelta = new Vector2(0, 30);
            var text = label.GetComponent<Text>();
            text.text = "임시 로딩 화면";
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(.75f, .75f, .75f, 1);
            text.raycastTarget = false;
            var track = new GameObject("Progress Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(_overlay.transform, false);
            var rect = (RectTransform)track.transform;
            rect.anchorMin = new Vector2(.1f, .06f);
            rect.anchorMax = new Vector2(.9f, .06f);
            rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(0, 12);
            track.GetComponent<Image>().color = new Color(.16f, .16f, .16f, 1);
            var fill = new GameObject("Progress Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            _fill = (RectTransform)fill.transform;
            Stretch(_fill);
            fill.GetComponent<Image>().color = new Color(.85f, .85f, .85f, 1);
            Refresh();
        }

        public void Refresh()
        {
            if (_disposed)
                return;
            float progress = _progress();
            if (float.IsFinite(progress))
            {
                _fill.anchorMin = Vector2.zero;
                _fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            }
            else
            {
                // NGO 전원 준비처럼 수치 진행률이 없는 구간은 실제 진행률과 구분합니다.
                float position = Mathf.PingPong((float)(Time.realtimeSinceStartupAsDouble % 3) / 2, .75f);
                _fill.anchorMin = new Vector2(position, 0);
                _fill.anchorMax = new Vector2(position + .25f, 1);
            }

            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        }

        public void SetFocused(bool focused)
        {
            if (_disposed)
                return;
            _overlay.SetActive(focused);
            if (focused && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _released();
            UnityEngine.Object.Destroy(_overlay);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
