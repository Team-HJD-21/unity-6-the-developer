using System.Collections.Generic;
using TeamHjd.Game.Turrets;
using Tower;
using UnityEngine;

namespace TeamHjd.Game.Debugging
{
    /// <summary>
    /// Lightweight Play Mode controls for the isolated TurretTest scene.
    /// This intentionally replaces TowerManager and its production UI dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurretTestController : MonoBehaviour
    {
        private const float PanelWidth = 440f;

        private readonly List<TurretBase> _turrets = new();
        private ControlUnitStatus _controlUnit;
        private Vector2 _scrollPosition;
        private bool _isPanelVisible = true;

        private void Awake()
        {
            RefreshReferences();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _isPanelVisible = !_isPanelVisible;
            }
        }

        private void OnGUI()
        {
            if (!_isPanelVisible)
            {
                if (GUI.Button(new Rect(16f, 16f, 180f, 32f), "Open Turret Test (F1)"))
                {
                    _isPanelVisible = true;
                }

                return;
            }

            float panelHeight = Mathf.Min(Screen.height - 32f, 620f);
            GUILayout.BeginArea(new Rect(16f, 16f, PanelWidth, panelHeight), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Turret Test Controls (F1)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Hide", GUILayout.Width(64f)))
            {
                _isPanelVisible = false;
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
            GUILayout.Label("Add a Monster-layer target to the scene to test tracking and firing.");
            GUILayout.EndArea();
        }

        private void DrawTurretRow(TurretBase turret)
        {
            string level = turret.Definition == null ? "?" : turret.Definition.Level.ToString();
            string power = turret.Definition == null ? "?" : turret.Definition.Power.ToString();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{turret.name}  |  LV {level}  |  Power {power}");
            GUILayout.BeginHorizontal();

            bool canActivate = turret is IActivateTower;
            GUI.enabled = canActivate;
            if (GUILayout.Button(turret.IsActivated ? "Deactivate" : "Activate", GUILayout.Width(100f)))
            {
                SetTurretActive(turret, !turret.IsActivated);
            }

            GUI.enabled = true;
            if (GUILayout.Button(turret.ShowRange ? "Hide Range" : "Show Range", GUILayout.Width(100f)))
            {
                turret.ShowRange = !turret.ShowRange;
            }

            GUILayout.Label(turret.IsActivated ? "ACTIVE" : "OFF");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void RefreshReferences()
        {
            _turrets.Clear();
            _turrets.AddRange(FindObjectsByType<TurretBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));
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

        private static void SetTurretActive(TurretBase turret, bool isActive)
        {
            if (turret is not IActivateTower activatableTurret)
            {
                return;
            }

            if (isActive)
            {
                activatableTurret.ActivateTurret();
            }
            else
            {
                activatableTurret.DeactivateTurret();
            }
        }
    }
}
