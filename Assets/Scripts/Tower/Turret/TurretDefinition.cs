using UnityEngine;
using UnityEngine.Serialization;

namespace TeamHjd.Game.Content
{
    // Each prefab has its own definition so stage-specific balancing stays independent.
    // Runtime state and upgrade-dependent damage are not stored in this asset.
    [CreateAssetMenu(fileName = "TurretDefinition", menuName = "The Developer/Turret Definition")]
    public sealed class TurretDefinition : ScriptableObject
    {
        [Header("Combat")]
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0f)] private float _range;
        [SerializeField, Min(0f)] private float _rotationSpeed;
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

        public int Level => _level;
        public float Range => _range;
        public float RotationSpeed => _rotationSpeed;
        public float FireRate => _fireRate;
        public int Power => _power;
        public float OverHeatTime => _overHeatTime;
        public int OverHeatMissileCount => _overHeatMissileCount;
        public float CoolTime => _coolTime;
    }
}
