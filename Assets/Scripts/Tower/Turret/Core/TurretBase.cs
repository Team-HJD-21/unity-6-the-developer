using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public abstract class TurretBase : MonoBehaviour
    {
        // Keep serialized field names unchanged so existing prefab values remain mapped.
        //제발 바꾸지 말아주세요요요요!
        [Header("Common References")]
        [SerializeField] protected Transform turret;
        [SerializeField] protected Transform turretRotationPoint;
        [SerializeField] protected LayerMask enemyMask;
        [SerializeField] protected LayerMask playerMask;
        [SerializeField] protected Animator animator;
        [SerializeField] protected SpriteRenderer gunRenderer;
        [SerializeField] protected SpriteRenderer rangeRenderer;
        [SerializeField] protected Transform rangeTransform;

        [Header("Definition and State")]
        [SerializeField] private TurretDefinition _definition;
        [SerializeField] private TurretRuntimeState _runtimeState = new();

        public TurretDefinition Definition => _definition;
        public TurretRuntimeState RuntimeState => _runtimeState;
        public int InstanceId => _runtimeState.InstanceId;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;
        public bool IsActivated => _runtimeState.IsActivated;
        public bool ShowRange { get; set; }

        protected bool ActivationChanged => _runtimeState.ActivationChanged;

        protected void SetActivated(bool isActivated)
        {
            _runtimeState.SetActivated(isActivated);
        }

        protected void CommitActivationState()
        {
            _runtimeState.CommitActivationState();
        }

        protected void SynchronizeActivationState(bool isActivated)
        {
            _runtimeState.SynchronizeActivationState(isActivated);
        }

        protected Transform TurretRotationPoint => turretRotationPoint;
        protected LayerMask EnemyMask => enemyMask;
        protected Animator Animator => animator;
        protected SpriteRenderer GunRenderer => gunRenderer;
        protected SpriteRenderer RangeRenderer => rangeRenderer;
        protected float Range => _definition.Range;
        protected float RotationSpeed => _definition.RotationSpeed;
        protected float FireRate => _definition.FireRate;
        protected int Power => _definition.Power;
        protected int Level => _definition.Level;
        protected int RPM => (int)(60 / (1 / FireRate));
        protected float TargetingAngle => _definition.TargetingAngle;

        // Runtime state is not configured in the Inspector.
        protected int Damage => Mathf.Max(0, _definition.Damage + _runtimeState.DamageBonus);
        protected float TimeTilFire;
        protected float TotCoolTime;
        protected GameObject OriginPower;
        protected ITurretPowerSource ControlUnitStatus;

        protected virtual void OnEnable()
        {
            TurretInstanceRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            TurretInstanceRegistry.Unregister(this);
        }

        public void SetDamageBonus(int damageBonus)
        {
            _runtimeState.SetDamageBonus(damageBonus);
        }

        public void AddDamageBonus(int damageBonus)
        {
            _runtimeState.AddDamageBonus(damageBonus);
        }
    }

}
