using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Turrets
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
