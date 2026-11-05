// 실제 미사일과 미발사 슬롯의 표적 claim을 관리하고 소유권을 이전·해제합니다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    /// <summary>
    /// Owns one missile's claim on a target. Transfer invalidates the previous handle;
    /// retaining a claim allows an intentional volley to share the same target.
    /// </summary>
    public sealed class MissileTargetReservation : IDisposable
    {
        private sealed class Entry
        {
            public readonly Transform Target;
            public int Count;

            public Entry(Transform target) => Target = target;
        }

        private static readonly Dictionary<Transform, Entry> Reservations = new();
        private Entry _entry;

        private MissileTargetReservation(Entry entry)
        {
            _entry = entry;
            entry.Count++;
        }

        public Transform Target => _entry?.Target;
        public bool IsValid => _entry != null && _entry.Target != null &&
                               _entry.Target.gameObject.activeInHierarchy &&
                               Reservations.TryGetValue(_entry.Target, out Entry current) &&
                               ReferenceEquals(current, _entry);

        public static bool TryAcquire(Transform target, out MissileTargetReservation reservation)
        {
            reservation = null;
            if (target == null || !target.gameObject.activeInHierarchy || Reservations.ContainsKey(target))
                return false;

            var entry = new Entry(target);
            Reservations.Add(target, entry);
            reservation = new MissileTargetReservation(entry);
            return true;
        }

        /// <summary>Host 체크포인트 참조 단계 전용입니다. 저장에 있던 같은 표적의 다중 claim을 다시 연결합니다.</summary>
        public static MissileTargetReservation RestoreCheckpointClaim(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) throw new ArgumentException("A restored target is required.", nameof(target));
            if (!Reservations.TryGetValue(target, out var entry)) Reservations.Add(target, entry = new Entry(target));
            return new MissileTargetReservation(entry);
        }

        public MissileTargetReservation Retain()
        {
            if (!IsValid)
                throw new InvalidOperationException("Cannot retain an invalid missile target reservation.");
            return new MissileTargetReservation(_entry);
        }

        public MissileTargetReservation Transfer()
        {
            if (!IsValid)
                throw new InvalidOperationException("Cannot transfer an invalid missile target reservation.");
            var nextOwner = new MissileTargetReservation(_entry);
            Dispose();
            return nextOwner;
        }

        public void Dispose()
        {
            Entry entry = _entry;
            _entry = null;
            if (entry == null || --entry.Count != 0)
                return;

            // A late release from a previous play session must not erase a new claim.
            if (Reservations.TryGetValue(entry.Target, out Entry current) && ReferenceEquals(current, entry))
                Reservations.Remove(entry.Target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reservations.Clear();
    }
}
