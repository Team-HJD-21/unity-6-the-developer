// Feature가 준비한 적용/백업 복구 동작을 실행기에 전달하는 기본 구현입니다.
using System;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class CheckpointRestoreStep : ICheckpointRestoreStep
    {
        private Action _apply, _rollback, _dispose;
        public CheckpointRestoreStep(Action apply, Action rollback, Action dispose = null)
        { _apply = apply ?? throw new ArgumentNullException(nameof(apply)); _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback)); _dispose = dispose; }
        public void Apply() { if (_apply == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _apply(); }
        public void Rollback() { if (_rollback == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _rollback(); }
        public void Dispose() { var dispose = _dispose; _apply = null; _rollback = null; _dispose = null; dispose?.Invoke(); }
    }
}
