using System.Collections.Generic;
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
        private const float MaximumPanelHeight = 700f;

        [Header("Debug UI")]
        [SerializeField, Range(1f, 2f)] private float uiScale = 1.25f;
        [SerializeField, Min(1)] private int damagePerClick = 25;

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
        private ControlUnitStatus _controlUnit;
        private Camera _mainCamera;
        private Vector2 _scrollPosition;
        private bool _isPanelVisible = true;
        private TurretActivationResult? _lastActivationResult;
        private string _lastDurabilityAction;

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
            float panelHeight = Mathf.Min(availableHeight, MaximumPanelHeight);
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

            if (_controlUnit != null)
            {
                GUILayout.Label($"ControlUnit Power: {_controlUnit.GetCurPower()} / {_controlUnit.GetMaxPower()}");
            }
            else
            {
                GUILayout.Label("ControlUnit: not found");
            }

            if (_lastActivationResult.HasValue)
            {
                GUILayout.Label($"Last Activation Request: {_lastActivationResult.Value}");
            }

            if (!string.IsNullOrEmpty(_lastDurabilityAction))
            {
                GUILayout.Label($"Last Durability Action: {_lastDurabilityAction}");
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
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            foreach (TurretBase turret in _turrets)
            {
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
            string power = turret.Definition == null ? "?" : turret.Definition.Power.ToString();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                $"{turret.name}  |  LV {level}  |  Power {power}  |  " +
                $"HP {turret.CurrentHealth}/{turret.MaxHealth}");
            GUILayout.BeginHorizontal();

            GUI.enabled = !turret.IsDestroyed;
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

            string status = turret.IsDestroyed
                ? "DESTROYED"
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
            GUILayout.EndVertical();
        }

        private void RefreshReferences()
        {
            _turrets.Clear();
            _turrets.AddRange(TurretInstanceRegistry.GetAll());
            _turrets.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            _controlUnit = FindFirstObjectByType<ControlUnitStatus>();
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
