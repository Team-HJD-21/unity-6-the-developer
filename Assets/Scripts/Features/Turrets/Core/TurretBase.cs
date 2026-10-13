using System.Collections;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public abstract class TurretBase : MonoBehaviour, ITurretActivationRequester, IDamageableTarget
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

        // Authored per prefab. Hidden from the normal Inspector and read-only to callers.
        [SerializeField, HideInInspector] private float minimumRange = 3f;

        [Header("Definition and State")]
        [SerializeField] private TurretDefinition _definition;
        [SerializeField] private TurretRuntimeState _runtimeState = new();

        private TurretActivationController _activationController;
        [SerializeField] private TurretController _turretController;
        private ITurretPowerSource _configuredPowerSource;
        private SpriteRenderer _minimumRangeRenderer;
        private float _lastVisualRange = -1f;
        private float _lastVisualMinimumRange = -1f;
        private TurretInitializationData _initializationData;
        private bool _initialActivationPending;
        private Coroutine _initialActivationRoutine;
        private float _coolingEndsAt;
        private float _coolingDuration;
        private float _coolingInitialHeatRatio;
        public bool IsInitialized => _activationController != null;
        // Shared by active overheat recovery and cooling after switching off.
        // IsActivated determines whether completion resumes attacks or only permits activation.
        public bool IsCoolingDown => Time.time < _coolingEndsAt;
        public float CoolingDownRemaining => Mathf.Max(0f, _coolingEndsAt - Time.time);
        public TurretActivationResult? LastInitialActivationResult { get; private set; }

        public TurretDefinition Definition => _definition;
        public virtual TurretKind Kind => TurretKind.Unknown;
        public TurretRuntimeState RuntimeState => _runtimeState;
        public int InstanceId => _runtimeState.InstanceId;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;
        public bool IsActivated => _runtimeState.IsActivated;
        public bool IsOperational => _runtimeState.IsOperational;
        public bool IsDestroyed => _runtimeState.IsDestroyed;
        public bool IsLocked => _runtimeState.IsLocked;
        public int CurrentHealth => _runtimeState.CurrentHealth;
        public int MaxHealth => _definition != null ? _definition.MaxHealth : 0;
        public int EffectiveDamage => _definition == null
            ? 0
            : _runtimeState.GetEffectiveDamage(_definition.Damage);
        public int EffectivePower => _definition == null
            ? 0
            : _runtimeState.GetEffectivePower(_definition.Power);
        public virtual int ProjectilesPerShot => 1;
        public float TheoreticalDps => _definition == null ? 0f : EffectiveDamage * _definition.FireRate * ProjectilesPerShot;
        public float DpsPerPower => EffectivePower > 0 ? TheoreticalDps / EffectivePower : 0f;
        public float EffectiveRange => _definition == null
            ? 0f
            : Mathf.Max(0f, _definition.Range * (1f + _runtimeState.RangeModifierRatio));
        public bool ShowRange { get; set; }
        // Spec upgrades change the outer radius only. Keep the physical dead zone fixed.
        public float MinimumRange => UsesMinimumRange
            ? Mathf.Clamp(minimumRange, 0f, EffectiveRange)
            : 0f;
        protected virtual bool UsesMinimumRange => false;

        protected Transform TurretRotationPoint => turretRotationPoint;
        protected LayerMask EnemyMask => enemyMask;
        protected Animator Animator => animator;
        protected SpriteRenderer GunRenderer => gunRenderer;
        protected SpriteRenderer RangeRenderer => rangeRenderer;
        protected float Range => EffectiveRange;
        protected float RotationSpeed => _definition.RotationSpeed;
        protected float FireRate => _definition.FireRate;
        protected int Power => EffectivePower;
        protected int Level => _definition.Level;
        protected int RPM => (int)(60 / (1 / FireRate));
        protected float TargetingAngle => _definition.TargetingAngle;

        // Runtime state is not configured in the Inspector.
        protected int Damage => EffectiveDamage;
        protected float TimeTilFire;

        /// <summary>버튼 요청 시점에만 호출한다. 동일 Rigidbody의 여러 Collider는 한 적으로 센다.</summary>
        public int CountEnemiesInAttackRange()
        {
            Vector2 origin = turret != null ? turret.position : transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, EffectiveRange, enemyMask);
            var enemies = new System.Collections.Generic.HashSet<int>();
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.gameObject.activeInHierarchy) continue;
                Transform target = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
                if (TurretTargetingUtility.IsInAttackRange(origin, target.position, MinimumRange, EffectiveRange))
                    enemies.Add(target.GetInstanceID());
            }
            return enemies.Count;
        }

        protected bool ConfigureSceneActivation()
        {
            if (_configuredPowerSource == null)
            {
                if (_turretController == null)
                    _turretController = TurretController.GetOrCreateForScene(gameObject.scene);
                _configuredPowerSource = _turretController;
            }
            return ConfigureActivation(_configuredPowerSource);
        }

        /// <summary>Only call on a new inactive instance, before its first Awake. Never overwrite a live turret.</summary>
        public void PrepareInitialization(TurretInitializationData data, ITurretPowerSource powerSource)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            data.Validate();
            if (gameObject.activeInHierarchy || IsInitialized || _initializationData != null)
                throw new System.InvalidOperationException("Initial state can only be prepared once on a new inactive turret.");
            if (_definition != data.Definition)
                throw new System.ArgumentException("Initialization definition must match the prefab.", nameof(data));
            if (powerSource == null || powerSource is Object unityObject && unityObject == null)
                throw new System.ArgumentNullException(nameof(powerSource));
            _initializationData = data;
            _configuredPowerSource = powerSource;
        }

        internal bool UsesPowerSource(ITurretPowerSource source) => ReferenceEquals(_configuredPowerSource, source);

        protected bool ConfigureActivation(ITurretPowerSource powerSource)
        {
            if (IsInitialized) return true;
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
            if (_initializationData != null)
            {
                _runtimeState.Initialize(_initializationData);
                shouldStartActivated = _initializationData.ShouldActivate;
            }
            _configuredPowerSource = powerSource;
            _runtimeState.SetActivated(false);
            _runtimeState.InitializeHealth(MaxHealth);
            _activationController = new TurretActivationController(
                _runtimeState,
                powerSource,
                Power);

            RefreshRangeVisual();

            _initialActivationPending = shouldStartActivated;

            return true;
        }

        protected void RefreshRangeVisual()
        {
            if (rangeTransform == null || rangeRenderer == null || rangeRenderer.sprite == null)
            {
                return;
            }

            // Sprite.bounds already accounts for Pixels Per Unit. Compensate for the
            // parent hierarchy so the rendered diameter matches the attack range.
            Vector2 spriteSize = rangeRenderer.sprite.bounds.size;
            Vector3 parentScale = rangeTransform.parent != null
                ? rangeTransform.parent.lossyScale
                : Vector3.one;
            float width = spriteSize.x * Mathf.Abs(parentScale.x);
            float height = spriteSize.y * Mathf.Abs(parentScale.y);
            if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
            {
                return;
            }

            float diameter = Range * 2f;
            Vector3 localScale = rangeTransform.localScale;
            localScale.x = diameter / width;
            localScale.y = diameter / height;
            rangeTransform.localScale = localScale;

            if (UsesMinimumRange && _minimumRangeRenderer == null)
            {
                GameObject innerCircle = new("MinimumRangeCircle");
                innerCircle.layer = rangeTransform.gameObject.layer;
                innerCircle.transform.SetParent(rangeTransform.parent, false);
                _minimumRangeRenderer = innerCircle.AddComponent<SpriteRenderer>();
            }

            if (_minimumRangeRenderer != null)
            {
                Transform innerTransform = _minimumRangeRenderer.transform;
                innerTransform.localPosition = rangeTransform.localPosition;
                innerTransform.localRotation = rangeTransform.localRotation;
                innerTransform.localScale = new Vector3(
                    MinimumRange * 2f / width, MinimumRange * 2f / height, localScale.z);
                _minimumRangeRenderer.sprite = rangeRenderer.sprite;
                _minimumRangeRenderer.sharedMaterial = rangeRenderer.sharedMaterial;
                _minimumRangeRenderer.sortingLayerID = rangeRenderer.sortingLayerID;
                _minimumRangeRenderer.sortingOrder = rangeRenderer.sortingOrder + 1;
                _minimumRangeRenderer.color = new Color(1f, 0.35f, 0.25f, rangeRenderer.color.a);
            }
            _lastVisualRange = Range;
            _lastVisualMinimumRange = MinimumRange;
        }

        protected void UpdateRangeVisibility()
        {
            if (_lastVisualRange != Range || _lastVisualMinimumRange != MinimumRange)
                RefreshRangeVisual();
            if (rangeRenderer != null)
                rangeRenderer.enabled = ShowRange;
            if (_minimumRangeRenderer != null)
                _minimumRangeRenderer.enabled = ShowRange && MinimumRange > 0f;
        }

        public bool IsManualActivationBlocked =>
            _configuredPowerSource is TurretController controller && controller.IsAutoActivationRunning;

        public TurretActivationResult RequestActivation(bool shouldActivate)
        {
            if (IsManualActivationBlocked)
            {
                Debug.Log("자동 활성화 진행 중에는 터렛을 수동으로 켜거나 끌 수 없습니다.", this);
                return TurretActivationResult.AutoActivationInProgress;
            }
            return RequestSystemActivation(shouldActivate);
        }

        internal TurretActivationResult RequestSystemActivation(bool shouldActivate)
        {
            // Any explicit request supersedes a queued startup intent.
            CancelInitialActivation();
            if (shouldActivate && IsDestroyed)
            {
                return TurretActivationResult.Destroyed;
            }

            if (shouldActivate && IsLocked)
            {
                return TurretActivationResult.Locked;
            }

            if (shouldActivate && !IsActivated && IsCoolingDown)
            {
                return TurretActivationResult.CoolingDown;
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

        public bool SetLocked(bool isLocked)
        {
            if (!isLocked && _configuredPowerSource is TurretController controller &&
                !controller.CanUnlock(this)) return false;
            return ApplyZoneLocked(isLocked);
        }

        internal bool ApplyZoneLocked(bool isLocked)
        {
            if (isLocked) CancelInitialActivation();
            if (_runtimeState.IsLocked == isLocked)
            {
                return false;
            }

            if (isLocked && IsActivated)
            {
                if (RequestSystemActivation(false) != TurretActivationResult.Deactivated)
                {
                    return false;
                }
            }

            _runtimeState.SetLocked(isLocked);
            TurretInstanceRegistry.NotifySnapshotChanged(this);
            return true;
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

        protected void BeginCooling(float duration, float initialHeatRatio = 1f)
        {
            // Switching off during cooling keeps the same deadline and heat progression.
            if (!IsCoolingDown)
            {
                _coolingDuration = Mathf.Max(0f, duration);
                _coolingInitialHeatRatio = Mathf.Clamp01(initialHeatRatio);
                _coolingEndsAt = Time.time + _coolingDuration;
            }

            TimeTilFire = 0f;
            if (Animator != null) Animator.SetBool("isShoot", false);
            SetTemporarilySuspended(IsActivated && IsCoolingDown);
            UpdateCooling();
        }

        protected virtual void Update()
        {
            if (_coolingDuration > 0f) UpdateCooling();
        }

        private void UpdateCooling()
        {
            float remaining = CoolingDownRemaining;
            float heatRatio = _coolingDuration > 0f
                ? _coolingInitialHeatRatio * remaining / _coolingDuration
                : 0f;
            ApplyCoolingHeat(heatRatio);
            if (GunRenderer != null)
                GunRenderer.color = new Color(1f, 1f - heatRatio, 1f - heatRatio);
            if (remaining > 0f) return;

            _coolingEndsAt = 0f;
            _coolingDuration = 0f;
            if (GunRenderer != null)
                GunRenderer.color = IsActivated ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            SetTemporarilySuspended(false);
        }

        protected virtual void ApplyCoolingHeat(float heatRatio) { }

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
            if (!IsInitialized) return;
            if (!TurretInstanceRegistry.Register(this))
            {
                enabled = false;
                return;
            }
            if (_initialActivationPending)
                _initialActivationRoutine = StartCoroutine(RequestInitialActivation());
        }

        protected virtual void OnDisable()
        {
            CancelInitialActivation();
            if (_minimumRangeRenderer != null)
                _minimumRangeRenderer.enabled = false;
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
            _initialActivationRoutine = null;
            if (!_initialActivationPending || !isActiveAndEnabled) yield break;
            _initialActivationPending = false;
            LastInitialActivationResult = RequestActivation(true);
        }

        private void CancelInitialActivation()
        {
            _initialActivationPending = false;
            if (_initialActivationRoutine == null) return;
            StopCoroutine(_initialActivationRoutine);
            _initialActivationRoutine = null;
        }

        public void SetDamageBonus(int damageBonus)
        {
            if (IsLocked) return;
            _runtimeState.SetDamageBonus(damageBonus);
            TurretInstanceRegistry.NotifySnapshotChanged(this);
        }

        public void AddDamageBonus(int damageBonus)
        {
            if (IsLocked) return;
            _runtimeState.AddDamageBonus(damageBonus);
            TurretInstanceRegistry.NotifySnapshotChanged(this);
        }

        public TurretUpgradeResult ApplyUpgrade(TurretUpgradeDefinition upgrade)
        {
            return ChangeUpgradeLevel(upgrade, 1);
        }

        public TurretUpgradeResult DowngradeUpgrade(TurretUpgradeDefinition upgrade)
        {
            return ChangeUpgradeLevel(upgrade, -1);
        }

        private TurretUpgradeResult ChangeUpgradeLevel(TurretUpgradeDefinition upgrade, int direction)
        {
            if (IsLocked)
                return TurretUpgradeResult.Locked;

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

            if (_runtimeState.UpgradeLevel > 0 && _runtimeState.SelectedUpgradeId != upgrade.Id)
            {
                return TurretUpgradeResult.BranchLocked;
            }

            int targetLevel = Mathf.Clamp(_runtimeState.UpgradeLevel + direction,
                0, TurretRuntimeState.MaximumUpgradeLevel);
            if (targetLevel == _runtimeState.UpgradeLevel)
                return direction > 0 ? TurretUpgradeResult.MaximumLevel : TurretUpgradeResult.MinimumLevel;

            if (IsDestroyed)
            {
                return TurretUpgradeResult.Destroyed;
            }

            if (_activationController == null)
            {
                return TurretUpgradeResult.NotInitialized;
            }

            // Compute from the base definition. Reversing Low Power can increase
            // the reservation and must fail atomically if CU power is insufficient.
            int upgradedPower = TurretRuntimeState.CalculateScaledStat(
                _definition.Power, upgrade.PowerModifierRatio * targetLevel);
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

            _runtimeState.SetUpgradeLevel(
                upgrade.Id,
                targetLevel,
                upgrade.DamageModifierRatio,
                upgrade.PowerModifierRatio,
                upgrade.RangeModifierRatio);
            RefreshRangeVisual();
            if (direction > 0)
                TurretInstanceRegistry.NotifyUpgradeApplied(this, upgrade);
            else
                TurretInstanceRegistry.NotifyUpgradeDowngraded(this, upgrade);
            return direction > 0 ? TurretUpgradeResult.Applied : TurretUpgradeResult.Downgraded;
        }

        public TurretLevelUpgradeResult RequestLevelUpgrade(
            TurretBase nextLevelPrefab, out TurretBase upgradedTurret)
        {
            return RequestLevelChange(nextLevelPrefab, 1, out upgradedTurret);
        }

        public TurretLevelUpgradeResult RequestLevelDowngrade(
            TurretBase previousLevelPrefab, out TurretBase downgradedTurret)
        {
            return RequestLevelChange(previousLevelPrefab, -1, out downgradedTurret);
        }

        private TurretLevelUpgradeResult RequestLevelChange(
            TurretBase targetLevelPrefab, int levelDelta, out TurretBase changedTurret)
        {
            changedTurret = null;
            if (IsLocked)
                return TurretLevelUpgradeResult.Locked;
            if (IsDestroyed)
                return TurretLevelUpgradeResult.Destroyed;
            if (_activationController == null || !isActiveAndEnabled)
                return TurretLevelUpgradeResult.NotInitialized;
            if (targetLevelPrefab == null || targetLevelPrefab.Definition == null ||
                _definition == null || targetLevelPrefab.Definition.Level != _definition.Level + levelDelta ||
                targetLevelPrefab.GetType().BaseType != GetType().BaseType)
                return levelDelta > 0
                    ? TurretLevelUpgradeResult.InvalidNextLevel
                    : TurretLevelUpgradeResult.InvalidPreviousLevel;

            bool wasActivated = IsActivated;
            bool showRange = ShowRange;
            int targetPower = _runtimeState.GetEffectivePower(targetLevelPrefab.Definition.Power);

            // Keep the replacement asleep until its ID and health have been transferred.
            GameObject staging = new GameObject("Turret Level Change Staging");
            staging.SetActive(false);
            GameObject replacementObject = Instantiate(
                targetLevelPrefab.gameObject, transform.position, transform.rotation, staging.transform);
            TurretBase replacement = replacementObject.GetComponent<TurretBase>();
            if (replacement == null)
            {
                Destroy(replacementObject);
                Destroy(staging);
                return levelDelta > 0
                    ? TurretLevelUpgradeResult.InvalidNextLevel
                    : TurretLevelUpgradeResult.InvalidPreviousLevel;
            }

            PowerCostChangeResult powerResult = _activationController.TrySetPowerCost(targetPower);
            if (powerResult != PowerCostChangeResult.Changed)
            {
                Destroy(replacementObject);
                Destroy(staging);
                return powerResult == PowerCostChangeResult.InsufficientPower
                    ? TurretLevelUpgradeResult.InsufficientPower
                    : TurretLevelUpgradeResult.PowerSourceUnavailable;
            }

            _runtimeState.CopyForLevelChange(
                replacement._runtimeState, MaxHealth, replacement.MaxHealth);
            replacement._configuredPowerSource = _configuredPowerSource;
            replacement._turretController = _turretController;
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
            replacement.RefreshRangeVisual();
            if (wasActivated)
            {
                replacement._activationController.AdoptReservationForLevelUpgrade();
                replacement.OnActivationChanged(true);
                TurretInstanceRegistry.NotifyActivationChanged(replacement, true);
                if (replacement.IsOperational)
                    TurretInstanceRegistry.NotifyOperationalStateChanged(replacement, true);
            }

            changedTurret = replacement;
            if (levelDelta > 0)
                TurretInstanceRegistry.NotifyLevelUpgraded(this, replacement);
            else
                TurretInstanceRegistry.NotifyLevelDowngraded(this, replacement);
            Destroy(gameObject);
            return levelDelta > 0
                ? TurretLevelUpgradeResult.Upgraded
                : TurretLevelUpgradeResult.Downgraded;
        }

        public bool ApplyDamage(int damage)
        {
            if (IsLocked) return false;
            if (!_runtimeState.ApplyDamage(damage))
            {
                return false;
            }

            TurretInstanceRegistry.NotifyHealthChanged(this, CurrentHealth, MaxHealth);
            if (CurrentHealth > 0)
            {
                return true;
            }

            RequestSystemActivation(false);
            _runtimeState.MarkDestroyed();
            OnDestroyed();
            TurretInstanceRegistry.NotifyDestroyed(this);
            return true;
        }

        public bool Restore()
        {
            if (IsLocked) return false;
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
