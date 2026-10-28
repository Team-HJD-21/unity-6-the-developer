using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamHJD.Game.Content
{
    // Each prefab has its own definition so stage-specific balancing stays independent.
    // Values that change while the game is running are stored in FieldTurretRuntimeState
    // or in the concrete turret implementation instead of this asset.
    [CreateAssetMenu(fileName = "FieldTurretDefinition", menuName = "The Developer/Field Turret Definition")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TeamHJD.Game.Content", sourceAssembly: "TeamHJD.Game.Turrets", sourceClassName: "TurretDefinition")]
    public sealed class FieldTurretDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private FieldTurretRole _role = FieldTurretRole.Weapon;
        [Header("Combat")]
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _damage;
        [SerializeField, Min(0f)] private float _range;
        [SerializeField, Min(0f)] private float _rotationSpeed;
        [SerializeField, Range(0f, 360f)] private float _targetingAngle = 10f;
        [FormerlySerializedAs("_shotsPerSecond")]
        [SerializeField, Min(0.01f)] private float _fireRate = 1f;

        [Header("Durability")]
        [SerializeField, Min(1)] private int _maxHealth = 100;

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
        public FieldTurretRole Role => _role;
        public int Level => _level;
        public int Damage => _damage;
        public float Range => _range;
        public float RotationSpeed => _rotationSpeed;
        public float TargetingAngle => _targetingAngle;
        public float FireRate => _fireRate;
        public int MaxHealth => _maxHealth;
        public int Power => _power;
        public float OverHeatTime => _overHeatTime;
        public int OverHeatMissileCount => _overHeatMissileCount;
        public float CoolTime => _coolTime;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall -= ValidateDefinitionIds;   //이미 예약돼 있으면 제거
            EditorApplication.delayCall += ValidateDefinitionIds;   //한 번 다시 예약
        }

        private static void ValidateDefinitionIds()
        {
            EditorApplication.delayCall -= ValidateDefinitionIds;

            var definitionsById = new Dictionary<string, FieldTurretDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:FieldTurretDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<FieldTurretDefinition>(path);
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    Debug.LogError($"Field Turret Definition ID is empty: {path}", definition);
                    continue;
                }

                if (definitionsById.TryGetValue(definition.Id, out FieldTurretDefinition duplicate))
                {
                    Debug.LogError(
                        $"Duplicate Field Turret Definition ID '{definition.Id}': " +
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
