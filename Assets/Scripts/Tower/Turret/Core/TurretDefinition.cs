using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamHJD.Game.Content
{
    // Each prefab has its own definition so stage-specific balancing stays independent.
    // Values that change while the game is running are stored in TurretRuntimeState
    // or in the concrete turret implementation instead of this asset.
    [CreateAssetMenu(fileName = "TurretDefinition", menuName = "The Developer/Turret Definition")]
    public sealed class TurretDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [Header("Combat")]
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _damage;
        [SerializeField, Min(0f)] private float _range;
        [SerializeField, Min(0f)] private float _rotationSpeed;
        [SerializeField, Range(0f, 360f)] private float _targetingAngle = 10f;
        [FormerlySerializedAs("_shotsPerSecond")]
        [SerializeField, Min(0.01f)] private float _fireRate = 1f;

        [Header("Power and Heat")]
        [FormerlySerializedAs("_powerCost")]
        [SerializeField, Min(0)] private int _power;
        [FormerlySerializedAs("_overheatSeconds")]
        [SerializeField, Min(0f)] private float _overHeatTime;
        [FormerlySerializedAs("_overheatMissileCount")]
        [SerializeField, Min(0)] private int _overHeatMissileCount;
        [FormerlySerializedAs("_cooldownSeconds")]
        [SerializeField, Min(0f)] private float _coolTime;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int Level => _level;
        public int Damage => _damage;
        public float Range => _range;
        public float RotationSpeed => _rotationSpeed;
        public float TargetingAngle => _targetingAngle;
        public float FireRate => _fireRate;
        public int Power => _power;
        public float OverHeatTime => _overHeatTime;
        public int OverHeatMissileCount => _overHeatMissileCount;
        public float CoolTime => _coolTime;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall -= ValidateDefinitionIds;
            EditorApplication.delayCall += ValidateDefinitionIds;
        }

        private static void ValidateDefinitionIds()
        {
            EditorApplication.delayCall -= ValidateDefinitionIds;

            var definitionsById = new Dictionary<string, TurretDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:TurretDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<TurretDefinition>(path);
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    Debug.LogError($"Turret Definition ID is empty: {path}", definition);
                    continue;
                }

                if (definitionsById.TryGetValue(definition.Id, out TurretDefinition duplicate))
                {
                    Debug.LogError(
                        $"Duplicate Turret Definition ID '{definition.Id}': " +
                        $"{AssetDatabase.GetAssetPath(duplicate)} and {path}",
                        definition);
                    continue;
                }

                definitionsById.Add(definition.Id, definition);
            }
        }
#endif
    }
}
