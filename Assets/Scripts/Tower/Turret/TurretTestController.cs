using System.Collections.Generic;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Debugging
{
    /// <summary>
    /// Lightweight Play Mode controls for the isolated TurretTest scene.
    /// This intentionally replaces TowerManager and its production UI dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurretTestController : MonoBehaviour
    {
        private const float PanelWidth = 520f;

        [Header("Debug UI")]
        [SerializeField, Range(1f, 2f)] private float uiScale = 1.25f;
        [SerializeField, Min(1)] private int damagePerClick = 25;

        [Header("Sample Upgrades")]
        [SerializeField] private TurretUpgradeDefinition[] sampleUpgrades;
        [SerializeField] private TurretLevelUpgradeCatalog levelUpgradeCatalog;

        [Header("Camera Movement")]
        [SerializeField, Min(0f)] private float cameraMoveSpeed = 8f;
        [SerializeField, Min(1f)] private float cameraFastMoveMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float cameraSpeedScrollStep = 1f;
        [SerializeField, Min(0.1f)] private float minimumCameraMoveSpeed = 1f;
        [SerializeField, Min(0.1f)] private float maximumCameraMoveSpeed = 30f;

        [Header("Camera Zoom")]
        [SerializeField, Min(0.1f)] private float cameraZoomSpeed = 6f;
        [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 2f;
        [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 20f;

        private readonly List<TurretBase> _turrets = new();
        private readonly Dictionary<int, TurretSnapshot> _capturedSnapshots = new();
        private TurretController _turretController;
        private TurretControllerSnapshot _controllerSnapshot;
        private Camera _mainCamera;
        private Vector2 _scrollPosition;
        private bool _isPanelVisible = true;
        private TurretActivationResult? _lastActivationResult;
        private string _lastDurabilityAction;
        private string _lastUpgradeAction;
        private string _lastLevelUpgradeAction;
        private string _lastLockAction;

        private void Awake()
        {
            RefreshReferences();
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            TurretInstanceRegistry.Registered += HandleRegistryChanged;
            TurretInstanceRegistry.Unregistered += HandleRegistryChanged;
            RefreshReferences();
        }

        private void OnDisable()
        {
            TurretInstanceRegistry.Registered -= HandleRegistryChanged;
            TurretInstanceRegistry.Unregistered -= HandleRegistryChanged;
        }

        private void Update()
        {
            AdjustCameraMoveSpeed();
            MoveCamera();
            ZoomCamera();

            if (GameInput.WasPressedThisFrame(GameKey.F1))
            {
                _isPanelVisible = !_isPanelVisible;
            }
        }

        private void OnGUI()
        {
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            float appliedUiScale = Mathf.Max(1f, uiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(appliedUiScale, appliedUiScale, 1f));

            if (!_isPanelVisible)
            {
                if (GUI.Button(new Rect(16f, 16f, 180f, 32f), "Open Turret Test (F1)"))
                {
                    _isPanelVisible = true;
                }

                GUI.matrix = previousGuiMatrix;
                return;
            }

            float availableHeight = Screen.height / appliedUiScale - 32f;
            float panelHeight = Mathf.Max(0f, availableHeight);
            GUILayout.BeginArea(new Rect(16f, 16f, PanelWidth, panelHeight), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Turret Test Controls (F1)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Hide", GUILayout.Width(64f)))
            {
                _isPanelVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show All Ranges"))
            {
                SetAllRangesVisible(true);
            }

            if (GUILayout.Button("Hide All Ranges"))
            {
                SetAllRangesVisible(false);
            }
            GUILayout.EndHorizontal();

            if (_controllerSnapshot != null)
            {
                GUILayout.Label($"Turret Power: {_controllerSnapshot.AvailablePower} / {_controllerSnapshot.MaximumPower}");
                GUILayout.Label($"Reserved: {_controllerSnapshot.ReservedPower} / Recovering: {_controllerSnapshot.PendingRecoveryPower}");
                GUILayout.Label($"Controller Turrets: {_controllerSnapshot.RegisteredTurretCount} / " +
                    $"Activated: {_controllerSnapshot.ActivatedTurretCount} / Operational: {_controllerSnapshot.OperationalTurretCount}");
                GUILayout.Label($"Activated Types: Canon {_controllerSnapshot.GetActivatedCount(TurretKind.Canon)} / " +
                    $"Missile {_controllerSnapshot.GetActivatedCount(TurretKind.Missile)} / " +
                    $"Laser {_controllerSnapshot.GetActivatedCount(TurretKind.Laser)} / " +
                    $"Tesla {_controllerSnapshot.GetActivatedCount(TurretKind.Tesla)} / " +
                    $"Unknown {_controllerSnapshot.GetActivatedCount(TurretKind.Unknown)}");
            }
            else
            {
                GUILayout.Label("TurretController: not found");
            }

            if (_lastActivationResult.HasValue)
            {
                GUILayout.Label($"Last Activation Request: {_lastActivationResult.Value}");
            }

            if (!string.IsNullOrEmpty(_lastDurabilityAction))
            {
                GUILayout.Label($"Last Durability Action: {_lastDurabilityAction}");
            }

            if (!string.IsNullOrEmpty(_lastUpgradeAction))
            {
                GUILayout.Label($"Last Upgrade Request: {_lastUpgradeAction}");
            }

            if (!string.IsNullOrEmpty(_lastLevelUpgradeAction))
            {
                GUILayout.Label($"Last Level Change: {_lastLevelUpgradeAction}");
            }

            if (!string.IsNullOrEmpty(_lastLockAction))
            {
                GUILayout.Label($"Last Lock Action: {_lastLockAction}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                RefreshReferences();
            }

            if (GUILayout.Button("Activate All"))
            {
                SetAllTurretsActive(true);
            }

            if (GUILayout.Button("Deactivate All"))
            {
                SetAllTurretsActive(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label($"Snapshots: All {TurretInstanceRegistry.GetAllSnapshots().Count} / " +
                $"Active {TurretInstanceRegistry.GetActiveSnapshots().Count} / " +
                $"Operational {TurretInstanceRegistry.GetOperationalSnapshots().Count}");
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            for (int index = 0; index < _turrets.Count; index++)
            {
                TurretBase turret = _turrets[index];
                if (turret == null)
                {
                    continue;
                }

                DrawTurretRow(turret);
            }

            GUILayout.EndScrollView();
            string cameraStatus = _mainCamera == null
                ? $"Move Speed: {cameraMoveSpeed:0.0}"
                : $"Move Speed: {cameraMoveSpeed:0.0} / Zoom Size: {_mainCamera.orthographicSize:0.0}";
            GUILayout.Label(cameraStatus);
            GUILayout.Label("Camera: WASD / Fast: Shift / Speed: Mouse Wheel");
            GUILayout.Label("Zoom In: Q / Zoom Out: Space");
            GUILayout.Label("Add a Monster-layer target to the scene to test tracking and firing.");
            GUILayout.EndArea();
            GUI.matrix = previousGuiMatrix;
        }

        private void AdjustCameraMoveSpeed()
        {
            float scrollInput = GameInput.ScrollY;
            if (Mathf.Approximately(scrollInput, 0f))
            {
                return;
            }

            cameraMoveSpeed = Mathf.Clamp(
                cameraMoveSpeed + scrollInput * cameraSpeedScrollStep,
                minimumCameraMoveSpeed,
                maximumCameraMoveSpeed);
        }

        private void MoveCamera()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    return;
                }
            }

            Vector2 direction = Vector2.zero;

            if (GameInput.IsPressed(GameKey.W)) direction.y += 1f;
            if (GameInput.IsPressed(GameKey.S)) direction.y -= 1f;
            if (GameInput.IsPressed(GameKey.A)) direction.x -= 1f;
            if (GameInput.IsPressed(GameKey.D)) direction.x += 1f;

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            bool isFastMove = GameInput.IsPressed(GameKey.LeftShift) || GameInput.IsPressed(GameKey.RightShift);
            float speed = isFastMove
                ? cameraMoveSpeed * cameraFastMoveMultiplier
                : cameraMoveSpeed;

            Vector3 movement = new(direction.x, direction.y, 0f);
            _mainCamera.transform.position += movement * (speed * Time.unscaledDeltaTime);
        }

        private void ZoomCamera()
        {
            if (_mainCamera == null || !_mainCamera.orthographic)
            {
                return;
            }

            float zoomDirection = 0f;
            if (GameInput.IsPressed(GameKey.Q)) zoomDirection -= 1f;
            if (GameInput.IsPressed(GameKey.Space)) zoomDirection += 1f;

            _mainCamera.orthographicSize = Mathf.Clamp(
                _mainCamera.orthographicSize + zoomDirection * cameraZoomSpeed * Time.unscaledDeltaTime,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        private void DrawTurretRow(TurretBase turret)
        {
            string level = turret.Definition == null ? "?" : turret.Definition.Level.ToString();
            string power = turret.Definition == null ? "?" : turret.EffectivePower.ToString();
            string damage = turret.Definition == null ? "?" : turret.EffectiveDamage.ToString();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                $"{turret.name}  |  ID {turret.InstanceId}  |  LV {level}");
            GUILayout.Label($"Damage {damage}  |  Power {power}  |  " +
                $"HP {turret.CurrentHealth}/{turret.MaxHealth}");
            GUILayout.Label($"Attack Range {turret.MinimumRange:F1} ~ {turret.EffectiveRange:F1}");
            if (TurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out TurretSnapshot snapshot))
            {
                GUILayout.Label($"Snapshot: {snapshot.DefinitionId}  |  Pos {snapshot.Position.x:F1}, {snapshot.Position.y:F1}");
                GUILayout.Label(
                    $"Active {snapshot.IsActivated}  |  Operational {snapshot.IsOperational}  |  " +
                    $"Destroyed {snapshot.IsDestroyed}  |  Locked {snapshot.IsLocked}");
                GUILayout.Label(
                    $"HP {snapshot.CurrentHealth}/{snapshot.MaxHealth}  |  Damage {snapshot.EffectiveDamage}  |  " +
                    $"Range {snapshot.Range:F1}  |  Power {snapshot.EffectivePower}");
                GUILayout.Label($"Spec Branch: {snapshot.SelectedUpgradeId} / Rank {snapshot.UpgradeLevel}/5");
            }
            else
            {
                GUILayout.Label("Snapshot: not registered");
            }
            GUILayout.BeginHorizontal();

            GUI.enabled = !turret.IsDestroyed && (!turret.IsLocked || turret.IsActivated);
            if (GUILayout.Button(turret.IsActivated ? "Deactivate" : "Activate", GUILayout.Width(100f)))
            {
                SetTurretActive(turret, !turret.IsActivated);
            }

            GUI.enabled = true;
            if (GUILayout.Button(turret.ShowRange ? "Hide Range" : "Show Range", GUILayout.Width(100f)))
            {
                turret.ShowRange = !turret.ShowRange;
            }

            if (GUILayout.Button("Focus", GUILayout.Width(72f)))
            {
                FocusCameraOn(turret);
            }

            if (GUILayout.Button(turret.IsLocked ? "Unlock" : "Lock", GUILayout.Width(72f)))
            {
                bool changed = turret.SetLocked(!turret.IsLocked);
                string outcome = !changed ? "Unchanged" : turret.IsLocked ? "Locked" : "Unlocked";
                _lastLockAction = $"{turret.name}: {outcome}";
            }

            string status = turret.IsDestroyed
                ? "DESTROYED"
                : turret.IsLocked
                    ? "LOCKED"
                : turret.IsOperational
                    ? "ACTIVE"
                    : turret.IsActivated ? "SUSPENDED" : "OFF";
            GUILayout.Label(status);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !turret.IsDestroyed;
            if (GUILayout.Button($"Damage -{damagePerClick}", GUILayout.Width(112f)))
            {
                bool changed = turret.ApplyDamage(damagePerClick);
                _lastDurabilityAction = $"{turret.name}: Damage {(changed ? "applied" : "ignored")}";
            }

            if (GUILayout.Button("Destroy", GUILayout.Width(80f)))
            {
                bool changed = turret.ApplyDamage(turret.CurrentHealth);
                _lastDurabilityAction = $"{turret.name}: Destroy {(changed ? "applied" : "ignored")}";
            }

            GUI.enabled = turret.IsDestroyed;
            if (GUILayout.Button("Restore", GUILayout.Width(80f)))
            {
                bool changed = turret.Restore();
                _lastDurabilityAction = $"{turret.name}: Restore {(changed ? "applied" : "ignored")}";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Snapshot"))
            {
                if (TurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out TurretSnapshot captured))
                    _capturedSnapshots[turret.InstanceId] = captured;
            }
            if (GUILayout.Button("Request Activate"))
                SetTurretActive(turret, true);
            GUILayout.EndHorizontal();
            if (_capturedSnapshots.TryGetValue(turret.InstanceId, out TurretSnapshot before))
            {
                GUILayout.Label($"Captured ID {before.InstanceId}: {before.DefinitionId}");
                GUILayout.Label($"Before: HP {before.CurrentHealth}/{before.MaxHealth} / " +
                    $"Damage {before.EffectiveDamage} / Range {before.Range:F1} / Power {before.EffectivePower}");
                GUILayout.Label($"Before: Active {before.IsActivated} / Locked {before.IsLocked} / " +
                    $"Destroyed {before.IsDestroyed}");
                GUILayout.Label($"Before: Branch {before.SelectedUpgradeId} / Rank {before.UpgradeLevel}/5");
            }

            DrawUpgradeButtons(turret);
            DrawLevelChangeButtons(turret);
            GUILayout.EndVertical();
        }

        private void DrawLevelChangeButtons(TurretBase turret)
        {
            if (levelUpgradeCatalog == null)
                return;

            GUILayout.BeginHorizontal();
            GUI.enabled = !turret.IsDestroyed;
            if (levelUpgradeCatalog.TryGetNext(turret.Definition, out TurretBase nextPrefab) &&
                GUILayout.Button($"Level Up: LV {nextPrefab.Definition.Level}"))
            {
                string previousName = turret.name;
                int previousId = turret.InstanceId;
                TurretLevelUpgradeResult result =
                    turret.RequestLevelUpgrade(nextPrefab, out TurretBase replacement);
                _lastLevelUpgradeAction = result == TurretLevelUpgradeResult.Upgraded
                    ? $"{previousName}: {result} (ID {previousId} -> {replacement.InstanceId})"
                    : $"{previousName} (ID {previousId}): {result}";
            }
            if (levelUpgradeCatalog.TryGetPrevious(turret.Definition, out TurretBase previousPrefab) &&
                GUILayout.Button($"Level Down: LV {previousPrefab.Definition.Level}"))
            {
                string previousName = turret.name;
                int previousId = turret.InstanceId;
                TurretLevelUpgradeResult result =
                    turret.RequestLevelDowngrade(previousPrefab, out TurretBase replacement);
                _lastLevelUpgradeAction = result == TurretLevelUpgradeResult.Downgraded
                    ? $"{previousName}: {result} (ID {previousId} -> {replacement.InstanceId})"
                    : $"{previousName} (ID {previousId}): {result}";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawUpgradeButtons(TurretBase turret)
        {
            if (sampleUpgrades == null || sampleUpgrades.Length == 0)
            {
                return;
            }

            foreach (TurretUpgradeDefinition upgrade in sampleUpgrades)
            {
                if (upgrade == null || !upgrade.IsCompatibleWith(turret.Definition?.Id))
                {
                    continue;
                }

                TurretRuntimeState state = turret.RuntimeState;
                bool branchAvailable = state.UpgradeLevel == 0 || state.SelectedUpgradeId == upgrade.Id;
                GUILayout.BeginHorizontal();
                GUILayout.Label(upgrade.DisplayName, GUILayout.Width(150f));
                GUI.enabled = !turret.IsDestroyed && branchAvailable &&
                    state.UpgradeLevel < TurretRuntimeState.MaximumUpgradeLevel;
                if (GUILayout.Button("+1", GUILayout.Width(50f)))
                {
                    TurretUpgradeResult result = turret.ApplyUpgrade(upgrade);
                    _lastUpgradeAction = $"{turret.name} / {upgrade.DisplayName}: {result}";
                }
                GUI.enabled = !turret.IsDestroyed && state.HasAppliedUpgrade(upgrade.Id);
                if (GUILayout.Button("-1", GUILayout.Width(50f)))
                {
                    TurretUpgradeResult result = turret.DowngradeUpgrade(upgrade);
                    _lastUpgradeAction = $"{turret.name} / {upgrade.DisplayName}: {result}";
                }
                GUI.enabled = true;
                if (!branchAvailable)
                    GUILayout.Label("LOCKED");
                GUILayout.EndHorizontal();
                GUILayout.Label($"Per step: Damage {upgrade.DamageModifierRatio:+0%;-0%;0%} / " +
                    $"Power {upgrade.PowerModifierRatio:+0%;-0%;0%} / Range {upgrade.RangeModifierRatio:+0%;-0%;0%}");
            }

            GUI.enabled = true;
        }

        private void RefreshReferences()
        {
            _turrets.Clear();
            _turrets.AddRange(TurretInstanceRegistry.GetAll());
            // Prefab names and levels change during promotion/demotion, but the
            // logical instance ID is transferred to the replacement turret.
            _turrets.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));

            _turretController = TurretController.FindForScene(gameObject.scene);
        }

        private void LateUpdate()
        {
            // IMGUI can run multiple times per frame. Capture once after gameplay Updates.
            if (_isPanelVisible)
                _controllerSnapshot = _turretController != null ? _turretController.GetSnapshot() : null;
        }

        private void SetAllTurretsActive(bool isActive)
        {
            foreach (TurretBase turret in _turrets)
            {
                if (turret != null && turret.IsActivated != isActive)
                {
                    SetTurretActive(turret, isActive);
                }
            }
        }

        private void SetAllRangesVisible(bool isVisible)
        {
            foreach (TurretBase turret in _turrets)
            {
                if (turret != null)
                {
                    turret.ShowRange = isVisible;
                }
            }
        }

        private void FocusCameraOn(TurretBase turret)
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera == null || turret == null)
            {
                return;
            }

            Vector3 cameraPosition = _mainCamera.transform.position;
            Vector3 turretPosition = turret.transform.position;
            _mainCamera.transform.position = new Vector3(
                turretPosition.x,
                turretPosition.y,
                cameraPosition.z);
        }

        private void SetTurretActive(TurretBase turret, bool isActive)
        {
            if (turret == null)
            {
                return;
            }

            _lastActivationResult = turret.RequestActivation(isActive);
        }

        private void HandleRegistryChanged(TurretBase turret)
        {
            RefreshReferences();
        }
    }
}
