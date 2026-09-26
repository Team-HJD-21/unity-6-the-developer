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

        internal static void NotifyActivationChanged(TurretBase turret, bool isActivated)
        {
            ActivationChanged?.Invoke(turret, isActivated);
        }

        internal static void NotifyOperationalStateChanged(TurretBase turret, bool isOperational)
        {
            OperationalStateChanged?.Invoke(turret, isOperational);
        }

        internal static void NotifyHealthChanged(TurretBase turret, int currentHealth, int maxHealth)
        {
            HealthChanged?.Invoke(turret, currentHealth, maxHealth);
        }

        internal static void NotifyDestroyed(TurretBase turret)
        {
            Destroyed?.Invoke(turret);
        }

        internal static void NotifyRestored(TurretBase turret)
        {
            Restored?.Invoke(turret);
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
