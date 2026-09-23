using TeamHjd.Game.Content;
using UnityEngine;

namespace TeamHjd.Game.Turrets
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
        [SerializeField] protected bool isActivated;

        public TurretDefinition Definition => _definition;
        public bool IsActivated => isActivated;
        public bool ShowRange { get; set; }

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

        // Runtime state is not configured in the Inspector.
        protected bool PreviousIsActivated;
        protected string Name;
        protected int Damage;
        protected float TimeTilFire;
        protected float AngleThreshold = 10f;//If Missile turret this value changes into 360f
        protected float TotCoolTime;
        protected GameObject OriginPower;
        protected ControlUnitStatus ControlUnitStatus;
    }
}
