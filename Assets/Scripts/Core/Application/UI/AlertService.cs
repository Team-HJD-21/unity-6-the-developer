// 한 App의 로컬 알림을 짧게 순서대로 표시하고 같은 결과의 연속 등록을 막습니다.
// Presenter가 실시간을 전달하므로 게임 시간·입력 잠금·Feature 상태에 의존하지 않습니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Application
{
    public sealed class AlertService : IDisposable
    {
        public const int MaximumPending = 3;
        private readonly Queue<AlertMessage> _pending = new();
        private bool _disposed, _suppressed;
        private double _now, _startedAt;
        public AlertMessage Current { get; private set; }
        public double CurrentAge => Current == null ? 0 : Math.Max(0, _now - _startedAt);
        public int PendingCount => _pending.Count;

        /// <summary>실제 입력/요청 결과에서 호출합니다. false는 표시를 수락하지 않았다는 뜻이며 게임 실패가 아닙니다.</summary>
        public bool Show(string message, AlertTone tone = AlertTone.Error, string key = null)
        {
            if (_disposed || _suppressed || string.IsNullOrWhiteSpace(message) || !Enum.IsDefined(typeof(AlertTone), tone)) return false;
            string text = message.Trim();
            key = string.IsNullOrWhiteSpace(key) ? text : key;
            if (Current?.Key == key || _pending.Count >= MaximumPending) return false;
            foreach (var pending in _pending) if (pending.Key == key) return false;
            _pending.Enqueue(new AlertMessage(text, tone, key));
            return true;
        }

        /// <summary>App Presenter가 매 프레임 호출합니다. 전체 화면 전환 중에는 큐도 폐기해 이전 Scene의 결과를 재생하지 않습니다.</summary>
        public void Tick(double realTime, double displaySeconds = 2.2, bool suppressed = false)
        {
            if (_disposed) return;
            if (double.IsNaN(realTime) || double.IsInfinity(realTime) || realTime < 0) throw new ArgumentOutOfRangeException(nameof(realTime));
            if (double.IsNaN(displaySeconds) || double.IsInfinity(displaySeconds) || displaySeconds <= 0) throw new ArgumentOutOfRangeException(nameof(displaySeconds));
            if (realTime < _now) Clear();
            _now = realTime; _suppressed = suppressed;
            if (suppressed) { Clear(); return; }
            if (Current != null && CurrentAge >= displaySeconds) Current = null;
            if (Current == null && _pending.Count > 0)
            { Current = _pending.Dequeue(); _startedAt = _now; }
        }

        /// <summary>Scene 교체/화면 가림 시 표시와 대기를 함께 비웁니다.</summary>
        public void Clear() { Current = null; _pending.Clear(); }
        public void Dispose() { if (_disposed) return; _disposed = true; Clear(); }
    }
}
