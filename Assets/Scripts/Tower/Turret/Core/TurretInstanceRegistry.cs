using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public static class TurretInstanceRegistry
    {
        private static readonly Dictionary<int, TurretBase> Instances = new();
        private static readonly Dictionary<string, TurretDefinition> DefinitionsById = new();
        private static int _nextInstanceId = 1;

        public static event Action<TurretBase> Registered;
        public static event Action<TurretBase> Unregistered;
        public static event Action<TurretBase, bool> ActivationChanged;
        public static event Action<TurretBase, bool> OperationalStateChanged;
        public static event Action<TurretBase, int, int> HealthChanged;
        public static event Action<TurretBase> Destroyed;
        public static event Action<TurretBase> Restored;
        public static event Action<TurretBase, TurretUpgradeDefinition> UpgradeApplied;
        public static event Action<TurretBase, TurretBase> LevelUpgraded;
        public static event Action<TurretBase, TurretBase> LevelDowngraded;
        // The ID is the only event payload. Query again for the latest values.
        // After unregistration, TryGetSnapshot returns false for this ID.
        public static event Action<int> SnapshotChanged;

        public static IReadOnlyDictionary<int, TurretBase> RegisteredInstances => Instances;
        public static int Count => Instances.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instances.Clear();
            DefinitionsById.Clear();
            _nextInstanceId = 1;
            Registered = null;
            Unregistered = null;
            ActivationChanged = null;
            OperationalStateChanged = null;
            HealthChanged = null;
            Destroyed = null;
            Restored = null;
            UpgradeApplied = null;
            LevelUpgraded = null;
            LevelDowngraded = null;
            SnapshotChanged = null;
        }

        public static bool Register(TurretBase turret)
        {
            if (turret == null)
            {
                return false;
            }

            if (!ValidateDefinitionId(turret))
            {
                return false;
            }

            if (turret.RuntimeState.HasInstanceId &&
                Instances.TryGetValue(turret.RuntimeState.InstanceId, out TurretBase existing) &&
                existing == turret)
            {
                return true;
            }

            int instanceId = turret.RuntimeState.InstanceId;
            if (!turret.RuntimeState.HasInstanceId ||
                Instances.TryGetValue(instanceId, out TurretBase registeredTurret) && registeredTurret != turret)
            {
                if (turret.RuntimeState.HasInstanceId)
                {
                    Debug.LogWarning(
                        $"Turret Instance ID {instanceId} is already registered. " +
                        $"A new runtime ID will be assigned to {turret.name}.",
                        turret);
                }

                instanceId = AllocateInstanceId();
                turret.RuntimeState.AssignInstanceId(instanceId);
            }

            Instances[instanceId] = turret;
            SnapshotChanged?.Invoke(instanceId);
            Registered?.Invoke(turret);
            return true;
        }

        public static void Unregister(TurretBase turret)
        {
            if (turret == null || !turret.RuntimeState.HasInstanceId)
            {
                return;
            }

            int instanceId = turret.RuntimeState.InstanceId;
            if (Instances.TryGetValue(instanceId, out TurretBase registeredTurret) && registeredTurret == turret)
            {
                Instances.Remove(instanceId);
                SnapshotChanged?.Invoke(instanceId);
                Unregistered?.Invoke(turret);
            }
        }

        public static bool TryGet(int instanceId, out TurretBase turret)
        {
            return Instances.TryGetValue(instanceId, out turret);
        }

        public static IReadOnlyList<TurretBase> GetAll()
        {
            return new List<TurretBase>(Instances.Values);
        }

        public static IReadOnlyList<TurretBase> GetActive()
        {
            var activeTurrets = new List<TurretBase>();
            foreach (TurretBase turret in Instances.Values)
            {
                if (turret != null && turret.IsActivated && !turret.IsDestroyed)
                {
                    activeTurrets.Add(turret);
                }
            }

            return activeTurrets;
        }

        public static IReadOnlyList<TurretBase> GetOperational()
        {
            var operationalTurrets = new List<TurretBase>();
            foreach (TurretBase turret in Instances.Values)
            {
                if (turret != null && turret.IsOperational)
                {
                    operationalTurrets.Add(turret);
                }
            }

            return operationalTurrets;
        }

        public static bool TryGetSnapshot(int instanceId, out TurretSnapshot snapshot)
        {
            if (Instances.TryGetValue(instanceId, out TurretBase turret) && turret != null)
            {
                snapshot = CreateSnapshot(turret);
                return true;
            }

            snapshot = default;
            return false;
        }

        public static bool TryGetInstanceId(Transform child, out int instanceId)
        {
            TurretBase turret = child != null ? child.GetComponentInParent<TurretBase>(true) : null;
            if (turret != null && turret.InstanceId > 0)
            {
                instanceId = turret.InstanceId;
                return true;
            }

            instanceId = 0;
            return false;
        }

        public static IReadOnlyList<TurretSnapshot> GetAllSnapshots()
        {
            return GetSnapshots(false, false);
        }

        public static IReadOnlyList<TurretSnapshot> GetActiveSnapshots()
        {
            return GetSnapshots(true, false);
        }

        public static IReadOnlyList<TurretSnapshot> GetOperationalSnapshots()
        {
            return GetSnapshots(false, true);
        }

        internal static void NotifyActivationChanged(TurretBase turret, bool isActivated)
        {
            NotifySnapshotChanged(turret);
            ActivationChanged?.Invoke(turret, isActivated);
        }

        internal static void NotifyOperationalStateChanged(TurretBase turret, bool isOperational)
        {
            NotifySnapshotChanged(turret);
            OperationalStateChanged?.Invoke(turret, isOperational);
        }

        internal static void NotifyHealthChanged(TurretBase turret, int currentHealth, int maxHealth)
        {
            NotifySnapshotChanged(turret);
            HealthChanged?.Invoke(turret, currentHealth, maxHealth);
        }

        internal static void NotifyDestroyed(TurretBase turret)
        {
            NotifySnapshotChanged(turret);
            Destroyed?.Invoke(turret);
        }

        internal static void NotifyRestored(TurretBase turret)
        {
            NotifySnapshotChanged(turret);
            Restored?.Invoke(turret);
        }

        internal static void NotifyUpgradeApplied(
            TurretBase turret,
            TurretUpgradeDefinition upgrade)
        {
            NotifySnapshotChanged(turret);
            UpgradeApplied?.Invoke(turret, upgrade);
        }

        internal static void NotifyLevelUpgraded(TurretBase previous, TurretBase current)
        {
            NotifySnapshotChanged(current);
            LevelUpgraded?.Invoke(previous, current);
        }

        internal static void NotifyLevelDowngraded(TurretBase previous, TurretBase current)
        {
            NotifySnapshotChanged(current);
            LevelDowngraded?.Invoke(previous, current);
        }

        internal static void NotifySnapshotChanged(TurretBase turret)
        {
            if (turret != null &&
                Instances.TryGetValue(turret.InstanceId, out TurretBase registered) &&
                registered == turret)
            {
                SnapshotChanged?.Invoke(turret.InstanceId);
            }
        }

        private static IReadOnlyList<TurretSnapshot> GetSnapshots(bool activeOnly, bool operationalOnly)
        {
            var snapshots = new List<TurretSnapshot>(Instances.Count);
            foreach (TurretBase turret in Instances.Values)
            {
                if (turret == null ||
                    (activeOnly && (!turret.IsActivated || turret.IsDestroyed)) ||
                    (operationalOnly && !turret.IsOperational))
                {
                    continue;
                }

                snapshots.Add(CreateSnapshot(turret));
            }

            return snapshots.AsReadOnly();
        }

        private static TurretSnapshot CreateSnapshot(TurretBase turret)
        {
            return new TurretSnapshot(
                turret.InstanceId,
                turret.Definition.Id,
                turret.transform.position,
                turret.IsActivated,
                turret.IsOperational,
                turret.IsDestroyed,
                turret.IsLocked,
                turret.CurrentHealth,
                turret.MaxHealth,
                turret.EffectiveDamage,
                turret.EffectiveRange,
                turret.EffectivePower);
        }

        private static int AllocateInstanceId()
        {
            while (Instances.ContainsKey(_nextInstanceId))
            {
                _nextInstanceId++;
            }

            return _nextInstanceId++;
        }

        private static bool ValidateDefinitionId(TurretBase turret)
        {
            TurretDefinition definition = turret.Definition;
            if (definition == null)
            {
                Debug.LogError($"Turret Definition is missing on {turret.name}.", turret);
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                Debug.LogError($"Turret Definition ID is empty on {turret.name}.", turret);
                return false;
            }

            if (DefinitionsById.TryGetValue(definition.Id, out TurretDefinition registeredDefinition) &&
                registeredDefinition != definition)
            {
                Debug.LogError(
                    $"Duplicate Turret Definition ID '{definition.Id}' is used by " +
                    $"'{registeredDefinition.name}' and '{definition.name}'.",
                    turret);
                return false;
            }

            DefinitionsById[definition.Id] = definition;
            return true;
        }
    }
}
