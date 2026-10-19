using System.Collections.Generic;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace TeamHJD.Game.Debugging
{
    /// <summary>
    /// UI-only controls for exercising production TurretController and TurretBase APIs in TurretTest.
    /// Global controls stay in the side panel; clicking a turret opens its independent debug window.
    /// This test-scene UI forwards requests without owning gameplay rules.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class TurretTestController : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float TurretWindowWidth = 540f;
        private const float TurretWindowHeight = 720f;

        private sealed class TurretFeedback
        {
            public TurretActivationResult? Activation;
            public string Durability;
            public string Upgrade;
            public string LevelChange;
            public string Lock;
        }

        [Header("Debug UI")]
        [SerializeField, Range(1f, 2f)] private float uiScale = 1.25f;
        [SerializeField, Min(1)] private int damagePerClick = 25;
        [SerializeField, Min(1)] private int unlockZoneCount = 4;
        [SerializeField] private bool showUnlockZones = true;
        [SerializeField, Min(1)] private int unlockGridCellsX = TurretGridConfiguration.DefaultCellsX;
        [SerializeField, Min(1)] private int unlockGridCellsY = TurretGridConfiguration.DefaultCellsY;

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
        private readonly Dictionary<int, TurretFeedback> _turretFeedback = new();
        private TurretController _turretController;
        private TurretControllerSnapshot _controllerSnapshot;
        private Camera _mainCamera;
        private Vector2 _scrollPosition;
        private Vector2 _turretScrollPosition;
        private bool _isPanelVisible = true;
        private int _selectedTurretId;
        private Rect _turretWindowRect;
        private bool _hasTurretWindowPosition;
        private GUIStyle _wrappedLabel;

        private void Start()
        {
            ConfigureTestUnlockZones();
            RefreshReferences();
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            TurretInstanceRegistry.Registered += HandleRegistryChanged;
            TurretInstanceRegistry.Unregistered += HandleRegistryChanged;
            RefreshReferences();
        }

        private void ConfigureTestUnlockZones()
        {
            _turretController = TurretController.FindForScene(gameObject.scene);
            if (_turretController == null)
            {
                Debug.LogWarning("TurretTest: production TurretController was not created by any turret.", this);
                return;
            }

            if (!TryGetTestMapBounds(gameObject.scene, out Bounds mapBounds))
            {
                Debug.LogWarning("TurretTest: map Tilemap bounds were not found; unlock zones are unavailable.", this);
                return;
            }

            foreach (ControlUnitHealth cu in FindObjectsByType<ControlUnitHealth>(FindObjectsSortMode.None))
            {
                if (cu.gameObject.scene != gameObject.scene) continue;
                var grid = new TurretGridConfiguration(
                    mapBounds.center, mapBounds.size.x, mapBounds.size.y,
                    unlockGridCellsX, unlockGridCellsY, cu.transform.position.z);
                _turretController.ConfigureUnlockZones(grid, cu.transform.position, unlockZoneCount);
                return;
            }
            Debug.LogWarning("TurretTest: CU was not found; unlock zones are unavailable.", this);
        }

        private static bool TryGetTestMapBounds(Scene scene, out Bounds mapBounds)
        {
            Tilemap selectedMap = null;
            long largestArea = 0;
            foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tilemap.gameObject.scene != scene) continue;
                BoundsInt cells = tilemap.cellBounds;
                long area = (long)cells.size.x * cells.size.y;
                if (area <= largestArea) continue;
                selectedMap = tilemap;
                largestArea = area;
            }

            if (selectedMap == null || largestArea == 0)
            {
                mapBounds = default;
                return false;
            }

            BoundsInt cellBounds = selectedMap.cellBounds;
            mapBounds = default;
            bool hasPoint = false;
            foreach (int x in new[] { cellBounds.xMin, cellBounds.xMax })
            foreach (int y in new[] { cellBounds.yMin, cellBounds.yMax })
            {
                Vector3 point = selectedMap.CellToWorld(new Vector3Int(x, y, cellBounds.zMin));
                if (!hasPoint)
                {
                    mapBounds = new Bounds(point, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    mapBounds.Encapsulate(point);
                }
            }

            return hasPoint && mapBounds.size.x > 0f && mapBounds.size.y > 0f;
        }

        private void OnDrawGizmos()
        {
            if (!showUnlockZones || _turretController == null || _turretController.UnlockZones == null) return;
            TurretUnlockZones zones = _turretController.UnlockZones;
            TurretGridConfiguration grid = zones.Grid;
            for (int y = 0; y < grid.CellsY; y++)
            for (int x = 0; x < grid.CellsX; x++)
            {
                int zone = zones.GetZoneForCell(x, y);
                bool unlocked = zone <= _turretController.UnlockedZoneCount;
                Gizmos.color = unlocked ? new Color(0.1f, 0.9f, 0.25f, 0.08f)
                    : new Color(0.95f, 0.2f, 0.2f, 0.08f);
                Vector3 center = new Vector3((float)(grid.MinimumX + (x + 0.5d) * grid.CellWidth),
                    (float)(grid.MinimumY + (y + 0.5d) * grid.CellHeight), (float)grid.OriginZ);
                Vector3 size = new Vector3((float)grid.CellWidth, (float)grid.CellHeight, 0f);
                Gizmos.DrawCube(center, size);
                if (x == 0 || zones.GetZoneForCell(x - 1, y) != zone ||
                    y == 0 || zones.GetZoneForCell(x, y - 1) != zone)
                {
                    Gizmos.color = unlocked ? Color.green : Color.red;
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }

        private void OnDisable()
        {
            TurretInstanceRegistry.Registered -= HandleRegistryChanged;
            TurretInstanceRegistry.Unregistered -= HandleRegistryChanged;
            CloseTurretWindow();
        }

        private void Update()
        {
            HandleTurretSelection();
            if (!IsPointerOverDebugUi(GameInput.PointerPosition))
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
            bool previousEnabled = GUI.enabled;
            float appliedUiScale = Mathf.Max(1f, uiScale);
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(appliedUiScale, appliedUiScale, 1f));
                DrawUnlockZoneOverlay(appliedUiScale);
                if (_isPanelVisible)
                    DrawGlobalPanel(appliedUiScale);
                else if (GUI.Button(new Rect(16f, 16f, 180f, 32f), "Open Turret Test (F1)"))
                    _isPanelVisible = true;

                if (TryGetSelectedTurret(out TurretBase turret))
                {
                    UpdateTurretWindowRect(appliedUiScale);
                    string level = turret.Definition != null ? turret.Definition.Level.ToString() : "?";
                    string title = $"{turret.name} | ID {turret.InstanceId} | LV {level}";
                    _turretWindowRect = GUILayout.Window(
                        GetInstanceID(), _turretWindowRect, DrawTurretWindow, title);
                }
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUI.matrix = previousGuiMatrix;
            }
        }

        private static Rect GlobalPanelRect(float scale) => new(
            16f, 16f, Mathf.Min(PanelWidth, Mathf.Max(1f, Screen.width / scale - 32f)),
            Mathf.Max(1f, Screen.height / scale - 32f));

        private void DrawWrappedLabel(string text, params GUILayoutOption[] options)
        {
            _wrappedLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(text, _wrappedLabel, options);
        }

        private void DrawGlobalPanel(float appliedUiScale)
        {
            GUILayout.BeginArea(GlobalPanelRect(appliedUiScale), GUI.skin.box);

            GUILayout.BeginHorizontal();
            DrawWrappedLabel("Turret Test Controls (F1)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Hide", GUILayout.Width(64f)))
            {
                _isPanelVisible = false;
            }
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            DrawWrappedLabel("Click a turret in the map to open its controls.");
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
                DrawWrappedLabel($"Turret Power: {_controllerSnapshot.AvailablePower} / {_controllerSnapshot.MaximumPower}");
                DrawWrappedLabel($"Reserved: {_controllerSnapshot.ReservedPower} / Recovering: {_controllerSnapshot.PendingRecoveryPower}");
                DrawWrappedLabel($"Controller Turrets: {_controllerSnapshot.RegisteredTurretCount} / " +
                    $"Activated: {_controllerSnapshot.ActivatedTurretCount} / Operational: {_controllerSnapshot.OperationalTurretCount}");
                DrawWrappedLabel($"Activated Types: Canon {_controllerSnapshot.GetActivatedCount(TurretKind.Canon)} / " +
                    $"Missile {_controllerSnapshot.GetActivatedCount(TurretKind.Missile)} / " +
                    $"Laser {_controllerSnapshot.GetActivatedCount(TurretKind.Laser)} / " +
                    $"Tesla {_controllerSnapshot.GetActivatedCount(TurretKind.Tesla)} / " +
                    $"Unknown {_controllerSnapshot.GetActivatedCount(TurretKind.Unknown)}");
            }
            else
            {
                DrawWrappedLabel("TurretController: not found");
            }

            TurretController autoController = _turretController;
            if (autoController != null)
            {
                if (autoController.UnlockZones != null)
                {
                    DrawWrappedLabel($"Unlock Zones: {autoController.UnlockedZoneCount} / {autoController.UnlockZones.ZoneCount} (cell steps from CU)");
                    DrawWrappedLabel("Game view overlay: green = unlocked, red = locked; number = zone");
                    GUI.enabled = !autoController.IsAutoActivationRunning;
                    if (GUILayout.Button("Unlock Next Zone") &&
                        autoController.UnlockedZoneCount < autoController.UnlockZones.ZoneCount)
                        autoController.UnlockThroughZone(autoController.UnlockedZoneCount + 1);
                    if (GUILayout.Button("Reset Zones (Inspector count)")) ConfigureTestUnlockZones();
                    GUI.enabled = true;
                }
                bool previousEnabled = GUI.enabled;
                GUI.enabled = !autoController.IsAutoActivationRunning && autoController.AutoActivationCooldownRemaining <= 0f;
                if (GUILayout.Button("Auto Activate")) autoController.TryStartAutoActivation();
                GUI.enabled = previousEnabled;
                DrawWrappedLabel($"Auto: {(autoController.IsAutoActivationRunning ? "Running" : "Idle")} / Cooldown: {autoController.AutoActivationCooldownRemaining:F1}s");
                foreach (TurretController.AutoActivationCandidate candidate in autoController.AutoActivationCandidates)
                    DrawWrappedLabel($"Priority ID {candidate.InstanceId}: enemies at click {candidate.EnemyCount}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                RefreshReferences();
            }

            GUI.enabled = _turretController == null || !_turretController.IsAutoActivationRunning;
            if (GUILayout.Button("Activate All"))
            {
                SetAllTurretsActive(true);
            }

            if (GUILayout.Button("Deactivate All"))
            {
                SetAllTurretsActive(false);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            DrawWrappedLabel($"Snapshots: All {TurretInstanceRegistry.GetAllSnapshots().Count} / " +
                $"Active {TurretInstanceRegistry.GetActiveSnapshots().Count} / " +
                $"Operational {TurretInstanceRegistry.GetOperationalSnapshots().Count}");
            string cameraStatus = _mainCamera == null
                ? $"Move Speed: {cameraMoveSpeed:0.0}"
                : $"Move Speed: {cameraMoveSpeed:0.0} / Zoom Size: {_mainCamera.orthographicSize:0.0}";
            DrawWrappedLabel(cameraStatus);
            DrawWrappedLabel("Camera: WASD / Fast: Shift / Speed: Mouse Wheel");
            DrawWrappedLabel("Zoom In: Q / Zoom Out: Space");
            DrawWrappedLabel("Turret window: drag its title / Close or Esc");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTurretWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            DrawWrappedLabel("Selected turret controls", GUILayout.ExpandWidth(true));
            bool close = GUILayout.Button("Close (Esc)", GUILayout.Width(100f));
            GUILayout.EndHorizontal();
            if (close)
            {
                CloseTurretWindow();
                return;
            }

            if (TryGetSelectedTurret(out TurretBase turret))
            {
                _turretScrollPosition = GUILayout.BeginScrollView(_turretScrollPosition);
                DrawTurretControls(turret);
                DrawTurretFeedback(turret);
                GUILayout.EndScrollView();
            }
            GUI.DragWindow(new Rect(0f, 0f, _turretWindowRect.width, 22f));
        }

        private void UpdateTurretWindowRect(float scale)
        {
            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            float width = Mathf.Min(TurretWindowWidth, Mathf.Max(1f, screenWidth - 32f));
            float height = Mathf.Min(TurretWindowHeight, Mathf.Max(1f, screenHeight - 32f));
            if (!_hasTurretWindowPosition)
            {
                _turretWindowRect = new Rect(Mathf.Max(16f, screenWidth - width - 16f), 16f, width, height);
                _hasTurretWindowPosition = true;
            }
            _turretWindowRect.width = width;
            _turretWindowRect.height = height;
            _turretWindowRect.x = Mathf.Clamp(_turretWindowRect.x, 16f, Mathf.Max(16f, screenWidth - width - 16f));
            _turretWindowRect.y = Mathf.Clamp(_turretWindowRect.y, 16f, Mathf.Max(16f, screenHeight - height - 16f));
        }

        private bool TryGetSelectedTurret(out TurretBase turret)
        {
            turret = null;
            return _selectedTurretId > 0 && TurretInstanceRegistry.TryGet(_selectedTurretId, out turret) &&
                   turret != null && turret.gameObject.scene == gameObject.scene;
        }

        private void OpenTurretWindow(TurretBase turret)
        {
            if (turret == null || turret.gameObject.scene != gameObject.scene ||
                !TurretInstanceRegistry.TryGet(turret.InstanceId, out TurretBase registered) || registered != turret)
                return;
            if (_selectedTurretId != turret.InstanceId)
                _turretScrollPosition = Vector2.zero;
            _selectedTurretId = turret.InstanceId;
            UpdateTurretWindowRect(Mathf.Max(1f, uiScale));
        }

        private void CloseTurretWindow() => _selectedTurretId = 0;

        private bool IsPointerOverDebugUi(Vector2 screenPosition)
        {
            float scale = Mathf.Max(1f, uiScale);
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / scale;
            Rect panel = _isPanelVisible ? GlobalPanelRect(scale) : new Rect(16f, 16f, 180f, 32f);
            if (panel.Contains(guiPosition)) return true;
            if (_selectedTurretId <= 0) return false;
            UpdateTurretWindowRect(scale);
            return _turretWindowRect.Contains(guiPosition);
        }

        private void HandleTurretSelection()
        {
            if (GameInput.WasPressedThisFrame(GameKey.Escape))
                CloseTurretWindow();
            if (!GameInput.PrimaryPointerWasPressedThisFrame || IsPointerOverDebugUi(GameInput.PointerPosition))
                return;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || !_mainCamera.pixelRect.Contains(GameInput.PointerPosition)) return;

            Ray ray = _mainCamera.ScreenPointToRay(GameInput.PointerPosition);
            TurretBase selected = null;
            float closest = float.PositiveInfinity;
            foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(ray, float.PositiveInfinity, Physics2D.AllLayers))
            {
                TurretBase candidate = hit.collider.GetComponentInParent<TurretBase>();
                if (candidate == null || candidate.gameObject.scene != gameObject.scene || hit.distance >= closest)
                    continue;
                if (!TurretInstanceRegistry.TryGet(candidate.InstanceId, out TurretBase registered) || registered != candidate)
                    continue;
                selected = candidate;
                closest = hit.distance;
            }
            if (selected != null) OpenTurretWindow(selected);
            else CloseTurretWindow();
        }

        private TurretFeedback FeedbackFor(TurretBase turret)
        {
            if (!_turretFeedback.TryGetValue(turret.InstanceId, out TurretFeedback feedback))
                _turretFeedback.Add(turret.InstanceId, feedback = new TurretFeedback());
            return feedback;
        }

        private void DrawTurretFeedback(TurretBase turret)
        {
            if (!_turretFeedback.TryGetValue(turret.InstanceId, out TurretFeedback feedback)) return;
            if (feedback.Activation.HasValue) DrawWrappedLabel($"Last activation: {feedback.Activation.Value}");
            if (!string.IsNullOrEmpty(feedback.Durability)) DrawWrappedLabel(feedback.Durability);
            if (!string.IsNullOrEmpty(feedback.Upgrade)) DrawWrappedLabel(feedback.Upgrade);
            if (!string.IsNullOrEmpty(feedback.LevelChange)) DrawWrappedLabel(feedback.LevelChange);
            if (!string.IsNullOrEmpty(feedback.Lock)) DrawWrappedLabel(feedback.Lock);
        }

        private void DrawUnlockZoneOverlay(float appliedUiScale)
        {
            if (!showUnlockZones || _mainCamera == null || _turretController == null ||
                _turretController.UnlockZones == null)
                return;

            TurretUnlockZones zones = _turretController.UnlockZones;
            TurretGridConfiguration grid = zones.Grid;
            Color previousColor = GUI.color;
            float scale = Mathf.Max(1f, appliedUiScale);

            for (int y = 0; y < grid.CellsY; y++)
            for (int x = 0; x < grid.CellsX; x++)
            {
                float left = (float)(grid.MinimumX + x * grid.CellWidth);
                float right = (float)(left + grid.CellWidth);
                float bottom = (float)(grid.MinimumY + y * grid.CellHeight);
                float top = (float)(bottom + grid.CellHeight);
                float z = (float)grid.OriginZ;
                Vector3 p0 = _mainCamera.WorldToScreenPoint(new Vector3(left, bottom, z));
                Vector3 p1 = _mainCamera.WorldToScreenPoint(new Vector3(right, top, z));
                if (p0.z <= 0f || p1.z <= 0f) continue;

                float guiLeft = Mathf.Min(p0.x, p1.x) / scale;
                float guiTop = (Screen.height - Mathf.Max(p0.y, p1.y)) / scale;
                float width = Mathf.Abs(p1.x - p0.x) / scale;
                float height = Mathf.Abs(p1.y - p0.y) / scale;
                if (width < 2f || height < 2f) continue;

                int zone = zones.GetZoneForCell(x, y);
                bool unlocked = zone <= _turretController.UnlockedZoneCount;
                Color zoneColor = unlocked ? new Color(0.1f, 1f, 0.25f) : new Color(1f, 0.2f, 0.15f);
                GUI.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.14f);
                GUI.DrawTexture(new Rect(guiLeft, guiTop, width, height), Texture2D.whiteTexture);

                GUI.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.9f);
                const float border = 1.5f;
                GUI.DrawTexture(new Rect(guiLeft, guiTop, width, border), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(guiLeft, guiTop + height - border, width, border), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(guiLeft, guiTop, border, height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(guiLeft + width - border, guiTop, border, height), Texture2D.whiteTexture);

                if (width >= 24f && height >= 18f)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(guiLeft, guiTop, width, height), zone.ToString());
                }
            }

            GUI.color = previousColor;
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

        private void DrawTurretControls(TurretBase turret)
        {
            string level = turret.Definition == null ? "?" : turret.Definition.Level.ToString();
            string power = turret.Definition == null ? "?" : turret.EffectivePower.ToString();
            string damage = turret.Definition == null ? "?" : turret.EffectiveDamage.ToString();

            GUILayout.BeginVertical(GUI.skin.box);
            DrawWrappedLabel(
                $"{turret.name}  |  ID {turret.InstanceId}  |  LV {level}");
            DrawWrappedLabel($"Damage {damage}  |  Power {power}  |  " +
                $"HP {turret.CurrentHealth}/{turret.MaxHealth}");
            DrawWrappedLabel($"Attack Range {turret.MinimumRange:F1} ~ {turret.EffectiveRange:F1}");
            if (_turretController != null && _turretController.TryGetUnlockZone(turret, out int unlockZone))
                DrawWrappedLabel($"Unlock Zone {unlockZone} / available through {_turretController.UnlockedZoneCount}");
            if (TurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out TurretSnapshot snapshot))
            {
                DrawWrappedLabel($"Snapshot: {snapshot.DefinitionId}  |  Pos {snapshot.Position.x:F1}, {snapshot.Position.y:F1}");
                DrawWrappedLabel(
                    $"Active {snapshot.IsActivated}  |  Operational {snapshot.IsOperational}  |  " +
                    $"Destroyed {snapshot.IsDestroyed}  |  Locked {snapshot.IsLocked}");
                DrawWrappedLabel(
                    $"HP {snapshot.CurrentHealth}/{snapshot.MaxHealth}  |  Damage {snapshot.EffectiveDamage}  |  " +
                    $"Range {snapshot.Range:F1}  |  Power {snapshot.EffectivePower}");
                DrawWrappedLabel($"Spec Branch: {snapshot.SelectedUpgradeId} / Rank {snapshot.UpgradeLevel}/5");
            }
            else
            {
                DrawWrappedLabel("Snapshot: not registered");
            }
            GUILayout.BeginHorizontal();

            GUI.enabled = !turret.IsManualOperationBlocked && !turret.IsDestroyed &&
                !turret.IsLocked && (turret.IsActivated || !turret.IsCoolingDown);
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

            GUI.enabled = !turret.IsManualOperationBlocked;
            if (GUILayout.Button(turret.IsLocked ? "Unlock" : "Lock", GUILayout.Width(72f)))
            {
                bool changed = turret.SetLocked(!turret.IsLocked);
                string outcome = !changed ? "Unchanged" : turret.IsLocked ? "Locked" : "Unlocked";
                FeedbackFor(turret).Lock = $"{turret.name}: {outcome}";
            }

            string status = turret.IsDestroyed
                ? "DESTROYED"
                : turret.IsLocked
                    ? "LOCKED"
                : turret.IsOperational
                    ? "ACTIVE"
                    : turret.IsActivated ? "SUSPENDED" : "OFF";
            DrawWrappedLabel(status);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !turret.IsManualOperationBlocked && !turret.IsDestroyed && !turret.IsLocked;
            if (GUILayout.Button($"Damage -{damagePerClick}", GUILayout.Width(112f)))
            {
                bool changed = turret.ApplyDamage(damagePerClick);
                FeedbackFor(turret).Durability = $"{turret.name}: Damage {(changed ? "applied" : "ignored")}";
            }

            if (GUILayout.Button("Destroy", GUILayout.Width(80f)))
            {
                bool changed = turret.ApplyDamage(turret.CurrentHealth);
                FeedbackFor(turret).Durability = $"{turret.name}: Destroy {(changed ? "applied" : "ignored")}";
            }

            GUI.enabled = !turret.IsManualOperationBlocked && turret.IsDestroyed && !turret.IsLocked;
            if (GUILayout.Button("Restore", GUILayout.Width(80f)))
            {
                bool changed = turret.Restore();
                FeedbackFor(turret).Durability = $"{turret.name}: Restore {(changed ? "applied" : "ignored")}";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Snapshot"))
            {
                if (TurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out TurretSnapshot captured))
                    _capturedSnapshots[turret.InstanceId] = captured;
            }
            GUI.enabled = !turret.IsManualOperationBlocked;
            if (GUILayout.Button("Request Activate"))
                SetTurretActive(turret, true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_capturedSnapshots.TryGetValue(turret.InstanceId, out TurretSnapshot before))
            {
                DrawWrappedLabel($"Captured ID {before.InstanceId}: {before.DefinitionId}");
                DrawWrappedLabel($"Before: HP {before.CurrentHealth}/{before.MaxHealth} / " +
                    $"Damage {before.EffectiveDamage} / Range {before.Range:F1} / Power {before.EffectivePower}");
                DrawWrappedLabel($"Before: Active {before.IsActivated} / Locked {before.IsLocked} / " +
                    $"Destroyed {before.IsDestroyed}");
                DrawWrappedLabel($"Before: Branch {before.SelectedUpgradeId} / Rank {before.UpgradeLevel}/5");
            }

            DrawUpgradeButtons(turret);
            DrawLevelChangeButtons(turret);
            if (turret.IsManualOperationBlocked)
                DrawWrappedLabel("Manual operations blocked during automatic activation.");
            else if (turret.IsActivated)
                DrawWrappedLabel("Deactivate and finish cooling before changing upgrades.");
            else if (turret.IsCoolingDown)
                DrawWrappedLabel($"Cooling: {turret.CoolingDownRemaining:F1}s. Upgrades unavailable.");
            GUILayout.EndVertical();
        }

        private void DrawLevelChangeButtons(TurretBase turret)
        {
            if (levelUpgradeCatalog == null)
                return;

            GUILayout.BeginHorizontal();
            GUI.enabled = turret.CanModifyUpgrades;
            if (levelUpgradeCatalog.TryGetNext(turret.Definition, out TurretBase nextPrefab) &&
                GUILayout.Button($"Level Up: LV {nextPrefab.Definition.Level}"))
            {
                string previousName = turret.name;
                int previousId = turret.InstanceId;
                TurretLevelUpgradeResult result =
                    turret.RequestLevelUpgrade(nextPrefab, out TurretBase replacement);
                FeedbackFor(turret).LevelChange = result == TurretLevelUpgradeResult.Upgraded
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
                FeedbackFor(turret).LevelChange = result == TurretLevelUpgradeResult.Downgraded
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
                DrawWrappedLabel(upgrade.DisplayName, GUILayout.Width(150f));
                GUI.enabled = turret.CanModifyUpgrades && branchAvailable &&
                    state.UpgradeLevel < TurretRuntimeState.MaximumUpgradeLevel;
                if (GUILayout.Button("+1", GUILayout.Width(50f)))
                {
                    TurretUpgradeResult result = turret.ApplyUpgrade(upgrade);
                    FeedbackFor(turret).Upgrade = $"{turret.name} / {upgrade.DisplayName}: {result}";
                }
                GUI.enabled = turret.CanModifyUpgrades && state.HasAppliedUpgrade(upgrade.Id);
                if (GUILayout.Button("-1", GUILayout.Width(50f)))
                {
                    TurretUpgradeResult result = turret.DowngradeUpgrade(upgrade);
                    FeedbackFor(turret).Upgrade = $"{turret.name} / {upgrade.DisplayName}: {result}";
                }
                GUI.enabled = true;
                if (!branchAvailable)
                    DrawWrappedLabel("LOCKED");
                GUILayout.EndHorizontal();
                DrawWrappedLabel($"Per step: Damage {upgrade.DamageModifierRatio:+0%;-0%;0%} / " +
                    $"Power {upgrade.PowerModifierRatio:+0%;-0%;0%} / Range {upgrade.RangeModifierRatio:+0%;-0%;0%}");
            }

            GUI.enabled = true;
        }

        private void RefreshReferences()
        {
            _turrets.Clear();
            foreach (TurretBase turret in TurretInstanceRegistry.GetAll())
                if (turret != null && turret.gameObject.scene == gameObject.scene)
                    _turrets.Add(turret);
            // Prefab names and levels change during promotion/demotion, but the
            // logical instance ID is transferred to the replacement turret.
            _turrets.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));

            _turretController = TurretController.FindForScene(gameObject.scene);
        }

        private void LateUpdate()
        {
            // Resolve by logical ID after all Updates, including prefab replacement.
            if (_selectedTurretId > 0 && !TryGetSelectedTurret(out _))
                CloseTurretWindow();
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

            FeedbackFor(turret).Activation = turret.RequestActivation(isActive);
        }

        private void HandleRegistryChanged(TurretBase turret)
        {
            RefreshReferences();
        }
    }
}
