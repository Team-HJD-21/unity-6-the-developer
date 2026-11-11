// 우주선 출입 때 안내 문구·진행 바 없이 검은 화면으로 이동 구간을 가립니다.
// 실시간 기준 0.3초 암전 → 최소 0.3초 이동 → 0.3초 복귀이며 게임 정지·배속에 영향받지 않습니다.
// 실제 로드가 더 걸리면 완전히 가려진 상태를 유지하고, 실패 복구도 가린 뒤 화면을 되돌립니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    public sealed class SceneFadePresenter : IDisposable
    {
        public const double PhaseSeconds = .3;
        private readonly GameObject _overlay;
        private readonly CanvasGroup _group;
        private bool _disposed;
        public bool IsVisible { get; private set; }

        public SceneFadePresenter(Transform parent)
        {
            _overlay = new GameObject("Scene Travel Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _overlay.transform.SetParent(parent, false);
            var rect = (RectTransform)_overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _overlay.GetComponent<Image>().color = Color.black;
            _group = _overlay.GetComponent<CanvasGroup>();
            _group.alpha = 0;
            _overlay.SetActive(false);
        }

        /// <summary>Core 이동 조율자가 호출합니다. 이동 함수는 화면이 완전히 검어졌을 때만 실행합니다.</summary>
        public async Task RunAsync(Func<Task> move, CancellationToken cancellation)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SceneFadePresenter));
            if (move == null) throw new ArgumentNullException(nameof(move));
            if (IsVisible) throw new InvalidOperationException("A Scene fade is already running.");
            cancellation.ThrowIfCancellationRequested();
            IsVisible = true;
            _overlay.SetActive(true);
            KeepOnTop();
            try
            {
                await BlendAsync(0, 1, cancellation);
                double coveredUntil = Time.realtimeSinceStartupAsDouble + PhaseSeconds;
                try { await move(); }
                finally
                {
                    while (Time.realtimeSinceStartupAsDouble < coveredUntil)
                        await Awaitable.NextFrameAsync(cancellation);
                    // 도착 Camera의 LateUpdate/렌더 준비도 완전 암전 안에서 끝냅니다.
                    await Awaitable.NextFrameAsync(cancellation);
                    await BlendAsync(1, 0, cancellation);
                }
            }
            finally
            {
                IsVisible = false;
                if (!_disposed && _group != null && _overlay != null)
                {
                    _group.alpha = 0;
                    _overlay.SetActive(false);
                }
            }
        }

        public void KeepOnTop()
        {
            if (!_disposed && IsVisible) _overlay.transform.SetAsLastSibling();
        }

        private async Task BlendAsync(float from, float to, CancellationToken cancellation)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            _group.alpha = from;
            while (Time.realtimeSinceStartupAsDouble - start < PhaseSeconds)
            {
                cancellation.ThrowIfCancellationRequested();
                _group.alpha = Mathf.Lerp(from, to, (float)((Time.realtimeSinceStartupAsDouble - start) / PhaseSeconds));
                await Awaitable.NextFrameAsync(cancellation);
            }
            _group.alpha = to;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnityEngine.Object.Destroy(_overlay);
        }
    }
}
