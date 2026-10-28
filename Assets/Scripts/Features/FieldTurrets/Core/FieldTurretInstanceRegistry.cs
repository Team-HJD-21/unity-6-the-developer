using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public static class FieldTurretInstanceRegistry
    {
        private static readonly Dictionary<int, FieldTurretBase> Instances = new();
        private static readonly Dictionary<string, FieldTurretDefinition> DefinitionsById = new();
        private static int _nextInstanceId = 1;

        public static event Action<FieldTurretBase> Registered;
        public static event Action<FieldTurretBase> Unregistered;
        public static event Action<FieldTurretBase, bool> ActivationChanged;
        public static event Action<FieldTurretBase, bool> OperationalStateChanged;
        public static event Action<FieldTurretBase, int, int> HealthChanged;
        public static event Action<FieldTurretBase> Destroyed;
        public static event Action<FieldTurretBase> Restored;
        public static event Action<FieldTurretBase, FieldTurretUpgradeDefinition> UpgradeApplied;
        public static event Action<FieldTurretBase, FieldTurretUpgradeDefinition> UpgradeDowngraded;
        public static event Action<FieldTurretBase, FieldTurretBase> LevelUpgraded;
        public static event Action<FieldTurretBase, FieldTurretBase> LevelDowngraded;
        // The ID is the only event payload. Query again for the latest values.
        // After unregistration, TryGetSnapshot returns false for this ID.
        public static event Action<int> SnapshotChanged;

        public static IReadOnlyDictionary<int, FieldTurretBase> RegisteredInstances => Instances;
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
            UpgradeDowngraded = null;
            LevelUpgraded = null;
            LevelDowngraded = null;
            SnapshotChanged = null;
        }

        public static bool Register(FieldTurretBase turret)
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
                Instances.TryGetValue(turret.RuntimeState.InstanceId, out FieldTurretBase existing) &&
                existing == turret)
            {
                return true;
            }

            int instanceId = turret.RuntimeState.InstanceId;
            if (!turret.RuntimeState.HasInstanceId ||
                Instances.TryGetValue(instanceId, out FieldTurretBase registeredTurret) && registeredTurret != turret)
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

        public static void Unregister(FieldTurretBase turret)
        {
            if (turret == null || !turret.RuntimeState.HasInstanceId)
            {
                return;
            }

            int instanceId = turret.RuntimeState.InstanceId;
            if (Instances.TryGetValue(instanceId, out FieldTurretBase registeredTurret) && registeredTurret == turret)
            {
                Instances.Remove(instanceId);
                SnapshotChanged?.Invoke(instanceId);
                Unregistered?.Invoke(turret);
            }
        }

        public static bool TryGet(int instanceId, out FieldTurretBase turret)
        {
            return Instances.TryGetValue(instanceId, out turret);
        }

        public static IReadOnlyList<FieldTurretBase> GetAll()
        {
            return new List<FieldTurretBase>(Instances.Values);
        }

        public static IReadOnlyList<FieldTurretBase> GetActive()
        {
            var activeTurrets = new List<FieldTurretBase>();
            foreach (FieldTurretBase turret in Instances.Values)
            {
                if (turret != null && turret.IsActivated && !turret.IsDestroyed)
                {
                    activeTurrets.Add(turret);
                }
            }

            return activeTurrets;
        }

        public static IReadOnlyList<FieldTurretBase> GetOperational()
        {
            var operationalTurrets = new List<FieldTurretBase>();
            foreach (FieldTurretBase turret in Instances.Values)
            {
                if (turret != null && turret.IsOperational)
                {
                    operationalTurrets.Add(turret);
                }
            }

            return operationalTurrets;
        }

        public static bool TryGetSnapshot(int instanceId, out FieldTurretSnapshot snapshot)
        {
            if (Instances.TryGetValue(instanceId, out FieldTurretBase turret) && turret != null)
            {
                snapshot = CreateSnapshot(turret);
                return true;
            }

            snapshot = default;
            return false;
        }

        public static bool TryGetInstanceId(Transform child, out int instanceId)
        {
            FieldTurretBase turret = child != null ? child.GetComponentInParent<FieldTurretBase>(true) : null;
            if (turret != null && turret.InstanceId > 0)
            {
                instanceId = turret.InstanceId;
                return true;
            }

            instanceId = 0;
            return false;
        }

        public static IReadOnlyList<FieldTurretSnapshot> GetAllSnapshots()
        {
            return GetSnapshots(false, false);
        }

        public static IReadOnlyList<FieldTurretSnapshot> GetActiveSnapshots()
        {
            return GetSnapshots(true, false);
        }

        public static IReadOnlyList<FieldTurretSnapshot> GetOperationalSnapshots()
        {
            return GetSnapshots(false, true);
        }

        internal static void NotifyActivationChanged(FieldTurretBase turret, bool isActivated)
        {
            NotifySnapshotChanged(turret);
            ActivationChanged?.Invoke(turret, isActivated);
        }

        internal static void NotifyOperationalStateChanged(FieldTurretBase turret, bool isOperational)
        {
            NotifySnapshotChanged(turret);
            OperationalStateChanged?.Invoke(turret, isOperational);
        }

        internal static void NotifyHealthChanged(FieldTurretBase turret, int currentHealth, int maxHealth)
        {
            NotifySnapshotChanged(turret);
            HealthChanged?.Invoke(turret, currentHealth, maxHealth);
        }

        internal static void NotifyDestroyed(FieldTurretBase turret)
        {
            NotifySnapshotChanged(turret);
            Destroyed?.Invoke(turret);
        }

        internal static void NotifyRestored(FieldTurretBase turret)
        {
            NotifySnapshotChanged(turret);
            Restored?.Invoke(turret);
        }

        internal static void NotifyUpgradeApplied(
            FieldTurretBase turret,
            FieldTurretUpgradeDefinition upgrade)
        {
            NotifySnapshotChanged(turret);
            UpgradeApplied?.Invoke(turret, upgrade);
        }

        internal static void NotifyLevelUpgraded(FieldTurretBase previous, FieldTurretBase current)
        {
            NotifySnapshotChanged(current);
            LevelUpgraded?.Invoke(previous, current);
        }

        internal static void NotifyUpgradeDowngraded(FieldTurretBase turret, FieldTurretUpgradeDefinition upgrade)
        {
            NotifySnapshotChanged(turret);
            UpgradeDowngraded?.Invoke(turret, upgrade);
        }

        internal static void NotifyLevelDowngraded(FieldTurretBase previous, FieldTurretBase current)
        {
            NotifySnapshotChanged(current);
            LevelDowngraded?.Invoke(previous, current);
        }

        internal static void NotifySnapshotChanged(FieldTurretBase turret)
        {
            if (turret != null &&
                Instances.TryGetValue(turret.InstanceId, out FieldTurretBase registered) &&
                registered == turret)
            {
                SnapshotChanged?.Invoke(turret.InstanceId);
            }
        }

        private static IReadOnlyList<FieldTurretSnapshot> GetSnapshots(bool activeOnly, bool operationalOnly)
        {
            var snapshots = new List<FieldTurretSnapshot>(Instances.Count);
            foreach (FieldTurretBase turret in Instances.Values)
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

        private static FieldTurretSnapshot CreateSnapshot(FieldTurretBase turret)
        {
            return new FieldTurretSnapshot(
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
                turret.EffectivePower,
                turret.RuntimeState.SelectedUpgradeId,
                turret.RuntimeState.UpgradeLevel,
                turret.TheoreticalDps);
        }

        private static int AllocateInstanceId()
        {
            while (Instances.ContainsKey(_nextInstanceId))
            {
                _nextInstanceId++;
            }

            return _nextInstanceId++;
        }

        private static bool ValidateDefinitionId(FieldTurretBase turret)
        {
            FieldTurretDefinition definition = turret.Definition;
            if (definition == null)
            {
                Debug.LogError($"Field Turret Definition is missing on {turret.name}.", turret);
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                Debug.LogError($"Field Turret Definition ID is empty on {turret.name}.", turret);
                return false;
            }

            if (DefinitionsById.TryGetValue(definition.Id, out FieldTurretDefinition registeredDefinition) &&
                registeredDefinition != definition)
            {
                Debug.LogError(
                    $"Duplicate Field Turret Definition ID '{definition.Id}' is used by " +
                    $"'{registeredDefinition.name}' and '{definition.name}'.",
                    turret);
                return false;
            }

            DefinitionsById[definition.Id] = definition;
            return true;
        }
    }
}
