// 도구가 실제로 변경한 시간 배율만 복원합니다. 외부 변경 값은 덮어쓰지 않습니다.
using System;
namespace TeamHJD.Game.Editor
{
    public sealed class TimeScaleLease : IDisposable
    {
        private readonly Func<float> _read;
        private readonly Action<float> _write;
        private float _original, _applied;
        public bool IsOwned { get; private set; }
        public TimeScaleLease(Func<float> read, Action<float> write) { _read=read; _write=write; }
        public void Apply(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 8) throw new ArgumentOutOfRangeException(nameof(value));
            if (!IsOwned) { _original=_read(); IsOwned=true; }
            _applied=value; _write(value);
        }
        public void Dispose()
        {
            if (!IsOwned) return;
            if (Math.Abs(_read()-_applied) < 0.0001f) _write(_original);
            IsOwned=false;
        }
    }
}
