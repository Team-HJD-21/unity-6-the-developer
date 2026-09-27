using System.Collections;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public abstract class TurretBase : MonoBehaviour, ITurretActivationRequester
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

        private TurretActivationController _activationController;

        public TurretDefinition Definition => _definition;
        public TurretRuntimeState RuntimeState => _runtimeState;
        public int InstanceId => _runtimeState.InstanceId;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;
        public bool IsActivated => _runtimeState.IsActivated;
        public bool IsOperational => _runtimeState.IsOperational;
        public bool IsDestroyed => _runtimeState.IsDestroyed;
        public int CurrentHealth => _runtimeState.CurrentHealth;
        public int MaxHealth => _definition != null ? _definition.MaxHealth : 0;
        public int EffectiveDamage => _definition == null
            ? 0
            : Mathf.Max(0, _definition.Damage + _runtimeState.DamageBonus);
        public int EffectivePower => _definition == null
            ? 0
            : Mathf.Max(0, _definition.Power + _runtimeState.PowerBonus);
        public bool ShowRange { get; set; }

        protected Transform TurretRotationPoint => turretRotationPoint;
        protected LayerMask EnemyMask => enemyMask;
        protected Animator Animator => animator;
        protected SpriteRenderer GunRenderer => gunRenderer;
        protected SpriteRenderer RangeRenderer => rangeRenderer;
        protected float Range => _definition.Range;
        protected float RotationSpeed => _definition.RotationSpeed;
        protected float FireRate => _definition.FireRate;
        protected int Power => EffectivePower;
        protected int Level => _definition.Level;
        protected int RPM => (int)(60 / (1 / FireRate));
        protected float TargetingAngle => _definition.TargetingAngle;

        // Runtime state is not configured in the Inspector.
        protected int Damage => EffectiveDamage;
        protected float TimeTilFire;
        protected float TotCoolTime;

        protected bool ConfigureActivation(ITurretPowerSource powerSource)
        {
            if (_definition == null)
            {
                Debug.LogError($"Turret Definition is missing on {name}.", this);
                return false;
            }

            if (powerSource == null ||
                powerSource is Object unityObject && unityObject == null)
            {
                Debug.LogError($"Turret power source is missing on {name}.", this);
                return false;
            }

            bool shouldStartActivated = _runtimeState.IsActivated;
            _runtimeState.SetActivated(false);
            _runtimeState.InitializeHealth(MaxHealth);
            _activationController = new TurretActivationController(
                _runtimeState,
                powerSource,
                Power);

            if (shouldStartActivated)
            {
                StartCoroutine(RequestInitialActivation());
            }

            return true;
        }

        public TurretActivationResult RequestActivation(bool shouldActivate)
        {
            if (shouldActivate && IsDestroyed)
            {
                return TurretActivationResult.Destroyed;
            }

            if (_activationController == null)
            {
                return TurretActivationResult.PowerSourceUnavailable;
            }

            bool wasOperational = IsOperational;
            TurretActivationResult result =
                _activationController.RequestActivation(shouldActivate);

            if (result is TurretActivationResult.Activated or
                TurretActivationResult.Deactivated)
            {
                OnActivationChanged(shouldActivate);
                TurretInstanceRegistry.NotifyActivationChanged(this, shouldActivate);

                if (wasOperational != IsOperational)
                {
                    TurretInstanceRegistry.NotifyOperationalStateChanged(this, IsOperational);
                }
            }

            return result;
        }

        protected void SetTemporarilySuspended(bool isSuspended)
        {
            bool wasOperational = IsOperational;
            _runtimeState.SetTemporarilySuspended(isSuspended);

            if (wasOperational != IsOperational)
            {
                TurretInstanceRegistry.NotifyOperationalStateChanged(this, IsOperational);
            }
        }

        protected virtual void OnActivationChanged(bool isActivated)
        {
        }

        protected virtual void OnDestroyed()
        {
        }

        protected virtual void OnRestored()
        {
        }

        protected virtual void OnEnable()
        {
            if (!TurretInstanceRegistry.Register(this))
            {
                enabled = false;
            }
        }

        protected virtual void OnDisable()
        {
            bool wasOperational = IsOperational;
            bool wasActivated = IsActivated;

            if (_activationController != null &&
                _activationController.ReleaseForShutdown())
            {
                if (wasActivated)
                {
                    TurretInstanceRegistry.NotifyActivationChanged(this, false);
                }

                if (wasOperational)
                {
                    TurretInstanceRegistry.NotifyOperationalStateChanged(this, false);
                }
            }

            _runtimeState.SetTemporarilySuspended(false);
            TurretInstanceRegistry.Unregister(this);
        }

        private IEnumerator RequestInitialActivation()
        {
            yield return null;
            RequestActivation(true);
        }

        public void SetDamageBonus(int damageBonus)
        {
            _runtimeState.SetDamageBonus(damageBonus);
        }

        public void AddDamageBonus(int damageBonus)
        {
            _runtimeState.AddDamageBonus(damageBonus);
        }

        public TurretUpgradeResult ApplyUpgrade(TurretUpgradeDefinition upgrade)
        {
            if (_definition == null || upgrade == null)
            {
                return TurretUpgradeResult.MissingDefinition;
            }

            if (string.IsNullOrWhiteSpace(upgrade.Id))
            {
                return TurretUpgradeResult.InvalidUpgradeId;
            }

            if (!upgrade.IsCompatibleWith(_definition.Id))
            {
                return TurretUpgradeResult.IncompatibleDefinition;
            }

            if (_runtimeState.HasAppliedUpgrade(upgrade.Id))
            {
                return TurretUpgradeResult.AlreadyApplied;
            }

            if (IsDestroyed)
            {
                return TurretUpgradeResult.Destroyed;
            }

            if (_activationController == null)
            {
                return TurretUpgradeResult.NotInitialized;
            }

            int upgradedPower = Mathf.Max(0, EffectivePower + upgrade.PowerModifier);
            PowerCostChangeResult powerResult =
                _activationController.TrySetPowerCost(upgradedPower);

            switch (powerResult)
            {
                case PowerCostChangeResult.InsufficientPower:
                    return TurretUpgradeResult.InsufficientPower;
                case PowerCostChangeResult.PowerSourceUnavailable:
                    return TurretUpgradeResult.PowerSourceUnavailable;
                case PowerCostChangeResult.InvalidPowerCost:
                    return TurretUpgradeResult.InvalidPowerCost;
            }

            _runtimeState.ApplyUpgrade(
                upgrade.Id,
                upgrade.DamageModifier,
                upgrade.PowerModifier);
            TurretInstanceRegistry.NotifyUpgradeApplied(this, upgrade);
            return TurretUpgradeResult.Applied;
        }

        public TurretLevelUpgradeResult RequestLevelUpgrade(
            TurretBase nextLevelPrefab, out TurretBase upgradedTurret)
        {
            upgradedTurret = null;
            if (IsDestroyed)
                return TurretLevelUpgradeResult.Destroyed;
            if (_activationController == null || !isActiveAndEnabled)
                return TurretLevelUpgradeResult.NotInitialized;
            if (nextLevelPrefab == null || nextLevelPrefab.Definition == null ||
                _definition == null || nextLevelPrefab.Definition.Level != _definition.Level + 1 ||
                nextLevelPrefab.GetType().BaseType != GetType().BaseType)
                return TurretLevelUpgradeResult.InvalidNextLevel;

            bool wasActivated = IsActivated;
            bool showRange = ShowRange;
            int nextPower = Mathf.Max(
                0, nextLevelPrefab.Definition.Power + _runtimeState.PowerBonus);

            // Keep the replacement asleep until its ID and health have been transferred.
            GameObject staging = new GameObject("Turret Upgrade Staging");
            staging.SetActive(false);
            GameObject replacementObject = Instantiate(
                nextLevelPrefab.gameObject, transform.position, transform.rotation, staging.transform);
            TurretBase replacement = replacementObject.GetComponent<TurretBase>();
            if (replacement == null)
            {
                Destroy(replacementObject);
                Destroy(staging);
                return TurretLevelUpgradeResult.InvalidNextLevel;
            }

            PowerCostChangeResult powerResult = _activationController.TrySetPowerCost(nextPower);
            if (powerResult != PowerCostChangeResult.Changed)
            {
                Destroy(replacementObject);
                Destroy(staging);
                return powerResult == PowerCostChangeResult.InsufficientPower
                    ? TurretLevelUpgradeResult.InsufficientPower
                    : TurretLevelUpgradeResult.PowerSourceUnavailable;
            }

            _runtimeState.CopyForLevelUpgrade(
                replacement._runtimeState, MaxHealth, replacement.MaxHealth);
            Transform originalParent = transform.parent;
            _activationController.DetachReservationForLevelUpgrade();
            gameObject.SetActive(false);
            replacementObject.transform.SetParent(originalParent, true);
            Destroy(staging);

            if (!replacement.isActiveAndEnabled || replacement._activationController == null)
            {
                replacementObject.SetActive(false);
                Destroy(replacementObject);
                gameObject.SetActive(true);
                if (wasActivated)
                {
                    _activationController.AdoptReservationForLevelUpgrade();
                    _activationController.TrySetPowerCost(EffectivePower);
                    OnActivationChanged(true);
                    TurretInstanceRegistry.NotifyActivationChanged(this, true);
                    if (IsOperational)
                        TurretInstanceRegistry.NotifyOperationalStateChanged(this, true);
                }
                else
                {
                    _activationController.TrySetPowerCost(EffectivePower);
                }
                return TurretLevelUpgradeResult.ReplacementInitializationFailed;
            }

            replacement.ShowRange = showRange;
            if (wasActivated)
            {
                replacement._activationController.AdoptReservationForLevelUpgrade();
                replacement.OnActivationChanged(true);
                TurretInstanceRegistry.NotifyActivationChanged(replacement, true);
                if (replacement.IsOperational)
                    TurretInstanceRegistry.NotifyOperationalStateChanged(replacement, true);
            }

            upgradedTurret = replacement;
            TurretInstanceRegistry.NotifyLevelUpgraded(this, replacement);
            Destroy(gameObject);
            return TurretLevelUpgradeResult.Upgraded;
        }

        public bool ApplyDamage(int damage)
        {
            if (!_runtimeState.ApplyDamage(damage))
            {
                return false;
            }

            TurretInstanceRegistry.NotifyHealthChanged(this, CurrentHealth, MaxHealth);
            if (CurrentHealth > 0)
            {
                return true;
            }

            RequestActivation(false);
            _runtimeState.MarkDestroyed();
            OnDestroyed();
            TurretInstanceRegistry.NotifyDestroyed(this);
            return true;
        }

        public bool Restore()
        {
            if (!_runtimeState.Restore(MaxHealth))
            {
                return false;
            }

            OnRestored();
            TurretInstanceRegistry.NotifyHealthChanged(this, CurrentHealth, MaxHealth);
            TurretInstanceRegistry.NotifyRestored(this);
            return true;
        }
    }

}
