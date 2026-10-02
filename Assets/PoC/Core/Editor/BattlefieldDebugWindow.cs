// Play Mode Match snapshot을 읽어 Scene View에 Territory와 Frontline만 그립니다.

using System.Collections.Generic;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DomainEntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Editor
{
    public sealed class BattlefieldDebugWindow : EditorWindow
    {
        private enum ProjectionPlane { XY, XZ }

        private AppRoot _appRoot;
        private BattlefieldSpatialSnapshot _snapshot;
        private ProjectionPlane _projectionPlane;
        private float _planeOffset;
        private bool _showTerritory = true;
        private bool _showFrontline = true;
        private bool _showVertexLabels;

        [MenuItem("Tools/TeamHJD/Battlefield Debug")]
        private static void Open()
        {
            var window = GetWindow<BattlefieldDebugWindow>("Battlefield Debug");
            window.minSize = new Vector2(300f, 180f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneView;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneView;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Bind(null);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Play Mode의 실행 중 Match snapshot만 표시합니다. Edit Mode fixture는 생성하지 않습니다.", MessageType.Info);
            var selectedRoot = (AppRoot)EditorGUILayout.ObjectField("App Root", _appRoot, typeof(AppRoot), true);
            if (selectedRoot != _appRoot) Bind(selectedRoot);

            _showTerritory = EditorGUILayout.Toggle("Territory", _showTerritory);
            _showFrontline = EditorGUILayout.Toggle("Frontline", _showFrontline);
            _showVertexLabels = EditorGUILayout.Toggle("Vertex labels", _showVertexLabels);
            _projectionPlane = (ProjectionPlane)EditorGUILayout.EnumPopup("World plane", _projectionPlane);
            _planeOffset = EditorGUILayout.FloatField("Plane offset", _planeOffset);

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode에 진입한 뒤 Hierarchy에서 AppRoot를 지정하세요.", MessageType.Warning);
            else if (_appRoot == null || _snapshot == null)
                EditorGUILayout.HelpBox("연결된 AppRoot에 활성 Match snapshot이 없습니다.", MessageType.None);
            else
                EditorGUILayout.LabelField("Vertices / triangles / frontline", $"{_snapshot.Vertices.Count} / {_snapshot.Triangles.Count} / {_snapshot.FrontlineEdges.Count}");

            SceneView.RepaintAll();
        }

        private void Bind(AppRoot appRoot)
        {
            if (_appRoot != null) _appRoot.BattlefieldSnapshotChanged -= OnSnapshotChanged;
            _appRoot = appRoot;
            _snapshot = null;
            if (_appRoot != null)
            {
                _appRoot.BattlefieldSnapshotChanged += OnSnapshotChanged;
                _snapshot = _appRoot.CurrentBattlefieldSnapshot;
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSnapshotChanged(BattlefieldSpatialSnapshot snapshot)
        {
            _snapshot = snapshot;
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Bind(null);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _snapshot = null;
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (EditorApplication.isPlaying && _appRoot != null)
                _snapshot = _appRoot.CurrentBattlefieldSnapshot;
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawSceneView(SceneView sceneView)
        {
            if (!EditorApplication.isPlaying || _appRoot == null || _snapshot == null) return;

            var positions = new Dictionary<DomainEntityId, Vector3>(_snapshot.Vertices.Count);
            foreach (var vertex in _snapshot.Vertices)
                positions.Add(vertex.EntityId, ToWorldPosition(vertex.Position));

            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                if (_showTerritory) DrawTerritory(positions);
                if (_showFrontline) DrawFrontlines(positions);
                if (_showVertexLabels) DrawVertexLabels(positions);
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private void DrawTerritory(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = new Color(0.1f, 0.75f, 1f, 0.12f);
            foreach (var triangle in _snapshot.Triangles)
            {
                var polygon = new[]
                {
                    positions[triangle.VertexA],
                    positions[triangle.VertexB],
                    positions[triangle.VertexC]
                };
                SortAroundCentroid(polygon, _projectionPlane == ProjectionPlane.XY);
                Handles.DrawAAConvexPolygon(polygon);
                Handles.DrawAAPolyLine(1.5f, polygon[0], polygon[1], polygon[2], polygon[0]);
            }
        }

        private void DrawFrontlines(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = new Color(1f, 0.35f, 0.1f, 1f);
            foreach (var edge in _snapshot.FrontlineEdges)
                Handles.DrawAAPolyLine(3f, positions[edge.First], positions[edge.Second]);
        }

        private void DrawVertexLabels(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = Color.white;
            foreach (var vertex in _snapshot.Vertices)
                Handles.Label(positions[vertex.EntityId], vertex.EntityId.ToString());
        }

        private Vector3 ToWorldPosition(BattlefieldPoint point) => _projectionPlane == ProjectionPlane.XY
            ? new Vector3((float)point.X, (float)point.Y, _planeOffset)
            : new Vector3((float)point.X, _planeOffset, (float)point.Y);

        private static void SortAroundCentroid(Vector3[] points, bool isXYPlane)
        {
            var center = (points[0] + points[1] + points[2]) / 3f;
            var centerY = isXYPlane ? center.y : center.z;
            System.Array.Sort(points, (left, right) =>
            {
                var leftY = isXYPlane ? left.y : left.z;
                var rightY = isXYPlane ? right.y : right.z;
                var leftAngle = Mathf.Atan2(leftY - centerY, left.x - center.x);
                var rightAngle = Mathf.Atan2(rightY - centerY, right.x - center.x);
                return leftAngle.CompareTo(rightAngle);
            });
        }
    }
}
