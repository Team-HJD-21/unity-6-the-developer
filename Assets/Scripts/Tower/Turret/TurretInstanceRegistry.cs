using System.Collections.Generic;
using TeamHjd.Game.Content;
using UnityEngine;

namespace TeamHjd.Game.Turrets
{
    public static class TurretInstanceRegistry
    {
        private static readonly Dictionary<int, TurretBase> Instances = new();
        private static readonly Dictionary<string, TurretDefinition> DefinitionsById = new();
        private static int _nextInstanceId = 1;

        public static IReadOnlyDictionary<int, TurretBase> RegisteredInstances => Instances;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instances.Clear();
            DefinitionsById.Clear();
            _nextInstanceId = 1;
        }

        public static void Register(TurretBase turret)
        {
            if (turret == null)
            {
                return;
            }

            ValidateDefinitionId(turret);

            int instanceId = turret.RuntimeState.InstanceId;
            if (!turret.RuntimeState.HasInstanceId ||
                Instances.TryGetValue(instanceId, out TurretBase registeredTurret) && registeredTurret != turret)
            {
                instanceId = AllocateInstanceId();
                turret.RuntimeState.AssignInstanceId(instanceId);
            }

            Instances[instanceId] = turret;
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
            }
        }

        public static bool TryGet(int instanceId, out TurretBase turret)
        {
            return Instances.TryGetValue(instanceId, out turret);
        }

        private static int AllocateInstanceId()
        {
            while (Instances.ContainsKey(_nextInstanceId))
            {
                _nextInstanceId++;
            }

            return _nextInstanceId++;
        }

        private static void ValidateDefinitionId(TurretBase turret)
        {
            TurretDefinition definition = turret.Definition;
            if (definition == null)
            {
                Debug.LogError($"Turret Definition is missing on {turret.name}.", turret);
                return;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                Debug.LogError($"Turret Definition ID is empty on {turret.name}.", turret);
                return;
            }

            if (DefinitionsById.TryGetValue(definition.Id, out TurretDefinition registeredDefinition) &&
                registeredDefinition != definition)
            {
                Debug.LogError(
                    $"Duplicate Turret Definition ID '{definition.Id}' is used by " +
                    $"'{registeredDefinition.name}' and '{definition.name}'.",
                    turret);
                return;
            }

            DefinitionsById[definition.Id] = definition;
        }
    }
}
