// 공통 Alert를 텍스트로만 표시하고 HUD가 전달한 화면 경계 위에 배치합니다.
// 포커스·입력·포인터를 점유하지 않고 UI 계층을 실행 중 생성하지 않습니다.
using TeamHJD.Game.Application;
using TMPro;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AlertStripView : MonoBehaviour
    {
        public CanvasGroup Group;
        public TMP_Text Message;
        public Color InfoColor = new(.839f, .749f, .486f);
        public Color SuccessColor = new(.36f, .8f, .48f);
        public Color ErrorColor = new(.98f, .72f, .66f);
        [Min(.5f)] public float DisplaySeconds = 2.2f;
        [Min(.01f)] public float EnterSeconds = .12f, ExitSeconds = .18f;
        [Min(0)] public float DefaultBottom = 166, ClearanceGap = 12, TopPadding = 16;
        [Min(0)] public float MinimumScreenScale = .8f;
        [Min(1)] public float MinimumScreenWidth = 800;
        private bool _hasClearance;
        private float _occupiedTopScreenY;
        public bool IsConfigured => Group != null && Message != null;

        private void Awake() { if (Message != null) Message.maxVisibleLines = 2; Hide(); }

        /// <summary>AppScreenPresenter만 호출합니다. 게임 배속과 무관한 AlertService의 나이를 표시합니다.</summary>
        public void Render(AlertService service)
        {
            if (!IsConfigured) return;
            ApplyPlacement();
            var alert = service.Current;
            if (alert == null) { Hide(); return; }
            Message.text = alert.Text;
            // TMP의 이 값은 프리팹 직렬화 대상이 아니므로 실제 표시 시에도 보장합니다.
            Message.maxVisibleLines = 2;
            Message.color = alert.Tone == AlertTone.Error ? ErrorColor : alert.Tone == AlertTone.Success ? SuccessColor : InfoColor;
            Group.alpha = UiMotion.Sigmoid(Mathf.Clamp01(Mathf.Min((float)service.CurrentAge / Mathf.Max(.01f, EnterSeconds),
                (DisplaySeconds - (float)service.CurrentAge) / Mathf.Max(.01f, ExitSeconds))));
            Group.interactable = Group.blocksRaycasts = false;
        }

        /// <summary>로컬 HUD가 실제로 가리는 사각형의 위쪽 Screen 픽셀 Y를 전달합니다. 판정/단위 변환은 Application에 넣지 않습니다.</summary>
        /// <remarks>호출 즉시 반영하므로 HUD는 View의 새 사각형을 읽어 툴팁/드래그 디스크와 추가 겹침을 검사할 수 있습니다.</remarks>
        public void SetScreenBottomClearance(float occupiedTopScreenY)
        {
            if (float.IsNaN(occupiedTopScreenY) || float.IsInfinity(occupiedTopScreenY)) return;
            _hasClearance = true; _occupiedTopScreenY = occupiedTopScreenY;
            ApplyPlacement();
        }

        /// <summary>HUD 해제/Scene 전환 시 해당 HUD의 임시 경계를 지우고 프리팹 기본 위치로 복귀합니다.</summary>
        public void ResetBottomClearance() { _hasClearance = false; ApplyPlacement(); }

        private void ApplyPlacement()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            var bottomCenter = new Vector2(.5f, 0);
            rect.anchorMin = rect.anchorMax = rect.pivot = bottomCenter;
            float bottom = DefaultBottom;
            var canvas = GetComponentInParent<Canvas>();
            float amount = canvas != null ? Mathf.Max(1, MinimumScreenScale * Mathf.Min(1,
                Screen.width / Mathf.Max(1, MinimumScreenWidth)) / Mathf.Max(.001f, canvas.rootCanvas.scaleFactor)) : 1;
            rect.localScale = Vector3.one * amount;
            if (_hasClearance && canvas != null)
            {
                canvas = canvas.rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                float centerX = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(parent.rect.center)).x;
                // Edit Mode의 Screen.height는 실제 Canvas 화면 범위와 다를 수 있어 Canvas 자체 경계를 사용합니다.
                float bottomY = RectTransformUtility.WorldToScreenPoint(camera,
                    parent.TransformPoint(new Vector2(parent.rect.center.x, parent.rect.yMin))).y;
                float topY = RectTransformUtility.WorldToScreenPoint(camera,
                    parent.TransformPoint(new Vector2(parent.rect.center.x, parent.rect.yMax))).y;
                float screenY = Mathf.Clamp(_occupiedTopScreenY, Mathf.Min(bottomY, topY), Mathf.Max(bottomY, topY));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    new Vector2(centerX, screenY), camera, out var point))
                    bottom = Mathf.Max(bottom, point.y - parent.rect.yMin + ClearanceGap);
            }
            float maximum = Mathf.Max(0, parent.rect.height - rect.rect.height * amount - TopPadding);
            rect.anchoredPosition = new Vector2(0, Mathf.Clamp(bottom, 0, maximum));
        }

        public void Hide()
        {
            if (Group != null) { Group.alpha = 0; Group.interactable = Group.blocksRaycasts = false; }
            if (Message != null) Message.text = "";
        }
    }
}
