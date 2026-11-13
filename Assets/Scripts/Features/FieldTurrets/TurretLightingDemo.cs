using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TeamHJD.Game.Debugging
{
    [DisallowMultipleComponent]
    public sealed class TurretLightingDemo : MonoBehaviour
    {
        private enum Mode { Flat, Light, LightAndPost }

        [SerializeField] private Transform viewTarget;
        [SerializeField, Min(1f)] private float viewSize = 11f;
        [SerializeField] private GameObject lights;
        [SerializeField] private Volume postProcessing;

        private IDisposable _view;
        private CameraManager _cameraManager;
        private UniversalAdditionalCameraData _cameraData;
        private WeatherShaderApply _weather;
        private bool _previousPostProcessing;
        private bool _previousWeather;
        private Mode _mode;
        private GUIStyle _statusStyle;

        private void Update()
        {
            if (_view != null && (_cameraManager != CameraManager.Current || _cameraManager.ActiveTarget != viewTarget))
                Close();

            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (_view == null) Open();
                else Close();
            }

            if (_view != null && Input.GetKeyDown(KeyCode.F8))
                ApplyMode((Mode)(((int)_mode + 1) % 3));
        }

        private void Open()
        {
            CameraManager manager = CameraManager.Current;
            if (viewTarget == null || lights == null || postProcessing == null ||
                manager == null || manager.gameObject.scene != gameObject.scene || manager.ActiveTarget == null)
                return;

            if (!manager.OutputCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                return;

            _view = manager.PushView(viewTarget, 0f, viewSize);
            _cameraManager = manager;
            _cameraData = cameraData;
            _previousPostProcessing = cameraData.renderPostProcessing;
            if (manager.OutputCamera.TryGetComponent(out _weather))
            {
                _previousWeather = _weather.enabled;
                _weather.enabled = false;
            }
            ApplyMode(Mode.Flat);
        }

        private void ApplyMode(Mode mode)
        {
            _mode = mode;
            lights.SetActive(mode != Mode.Flat);
            bool post = mode == Mode.LightAndPost;
            postProcessing.enabled = post;
            _cameraData.renderPostProcessing = post;
        }

        private void Close()
        {
            if (_view == null) return;

            if (lights != null) lights.SetActive(false);
            if (postProcessing != null) postProcessing.enabled = false;
            if (_cameraData != null) _cameraData.renderPostProcessing = _previousPostProcessing;
            if (_weather != null) _weather.enabled = _previousWeather;
            _view.Dispose();
            _view = null;
        }

        private void OnDisable() => Close();

        private void OnGUI()
        {
            if (_view == null)
            {
                if (GUI.Button(new Rect(32f, 34f, 165f, 32f), "Lighting demo  [F7]"))
                    Open();
                return;
            }

            float scale = Mathf.Max(0.6f, Mathf.Min(Screen.width / 520f, 1.25f));
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalBackground = GUI.backgroundColor;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(30f, 38f, 420f, 96f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawMode(Mode.Flat, "Flat reference");
            DrawMode(Mode.Light, "Light 2D");
            DrawMode(Mode.LightAndPost, "Light 2D + Post");
            GUI.backgroundColor = originalBackground;
            bool close = GUILayout.Button("Exit", GUILayout.Width(47f), GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            _statusStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            string status = _mode switch
            {
                Mode.Light => "Light 2D   /   Ambient and local lights",
                Mode.LightAndPost => "Light 2D + Post   /   Bloom, contrast, vignette",
                _ => "Flat reference   /   Light and post off"
            };
            GUILayout.Label(status, _statusStyle, GUILayout.Height(23f));
            GUILayout.Label("F8  Next look     F7  Return to player", _statusStyle, GUILayout.Height(20f));
            GUILayout.EndArea();
            GUI.matrix = originalMatrix;
            if (close) Close();
        }

        private void DrawMode(Mode mode, string label)
        {
            GUI.backgroundColor = _mode == mode ? new Color(.63f, .85f, 1f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(30f)) && _mode != mode)
                ApplyMode(mode);
        }
    }
}
