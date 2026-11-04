// Feature가 준비한 적용·백업 복구와 actor 재생성 후의 참조 연결을 실행기에 전달합니다.
using System;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class CheckpointRestoreStep : ICheckpointReferenceRestoreStep
    {
        private Action _apply, _rollback, _dispose, _resolveReferences, _resolveRollbackReferences;
        public CheckpointRestoreStep(Action apply, Action rollback, Action dispose = null,
            Action resolveReferences = null, Action resolveRollbackReferences = null)
        {
            if ((resolveReferences == null) != (resolveRollbackReferences == null))
                throw new ArgumentException("Supply both target and rollback reference resolvers.");
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback)); _dispose = dispose;
            _resolveReferences = resolveReferences; _resolveRollbackReferences = resolveRollbackReferences;
        }
        public void Apply() { if (_apply == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _apply(); }
        public void Rollback() { if (_rollback == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _rollback(); }
        public void ResolveReferences() { if (_apply == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _resolveReferences?.Invoke(); }
        public void ResolveRollbackReferences() { if (_rollback == null) throw new ObjectDisposedException(nameof(CheckpointRestoreStep)); _resolveRollbackReferences?.Invoke(); }
        public void Dispose()
        {
            var dispose = _dispose; _apply = null; _rollback = null; _dispose = null;
            _resolveReferences = null; _resolveRollbackReferences = null; dispose?.Invoke();
        }
    }
}
