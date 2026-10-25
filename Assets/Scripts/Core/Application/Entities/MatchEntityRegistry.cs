// Feature별 로컬 핸들과 Match 논리 ID의 일대일 대응만 소유합니다.
// 실제 개체 생성·파괴나 네트워크 객체 소유권을 대신하지 않습니다. 메인 스레드 전용입니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MatchEntityRegistry<THandle> : IDisposable
    {
        private readonly Dictionary<EntityId, EntityRegistration<THandle>> _byEntity = new Dictionary<EntityId, EntityRegistration<THandle>>();
        private readonly Dictionary<THandle, EntityRegistration<THandle>> _byHandle = new Dictionary<THandle, EntityRegistration<THandle>>();
        private long _generation;
        public MatchId MatchId { get; }
        public bool IsDisposed { get; private set; }
        public int Count => _byEntity.Count;
        public MatchEntityRegistry(MatchId matchId)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            MatchId = matchId;
        }
        public EntityRegistration<THandle> Register(EntityId entityId, THandle handle)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MatchEntityRegistry<THandle>));
            if (entityId.IsEmpty) throw new ArgumentException("A logical entity ID is required.", nameof(entityId));
            if (ReferenceEquals(handle, null)) throw new ArgumentNullException(nameof(handle));
            if (_byEntity.TryGetValue(entityId, out var existing))
            {
                if (EqualityComparer<THandle>.Default.Equals(existing.Handle, handle)) return existing;
                throw new InvalidOperationException("The entity already has another local handle.");
            }
            if (_byHandle.ContainsKey(handle)) throw new InvalidOperationException("The local handle already belongs to another entity.");
            var registration = new EntityRegistration<THandle>(MatchId, entityId, handle, checked(++_generation));
            _byEntity.Add(entityId, registration); _byHandle.Add(handle, registration);
            return registration;
        }
        public bool TryGetByEntityId(EntityId entityId, out EntityRegistration<THandle> registration)
        { registration = null; return !IsDisposed && !entityId.IsEmpty && _byEntity.TryGetValue(entityId, out registration); }
        public bool TryGetByHandle(THandle handle, out EntityRegistration<THandle> registration)
        { registration = null; return !IsDisposed && !ReferenceEquals(handle, null) && _byHandle.TryGetValue(handle, out registration); }
        public bool Unregister(EntityRegistration<THandle> registration)
        {
            if (IsDisposed || registration == null || registration.MatchId != MatchId ||
                !_byEntity.TryGetValue(registration.EntityId, out var current) || !ReferenceEquals(current, registration)) return false;
            _byEntity.Remove(current.EntityId); _byHandle.Remove(current.Handle); return true;
        }
        public IReadOnlyList<EntityRegistration<THandle>> CaptureRegistrations()
            => new List<EntityRegistration<THandle>>(_byEntity.Values).AsReadOnly();
        public void Dispose() { if (IsDisposed) return; IsDisposed = true; _byEntity.Clear(); _byHandle.Clear(); }
    }
}
