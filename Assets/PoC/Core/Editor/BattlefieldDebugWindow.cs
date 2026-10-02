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
        private AppRoot _appRoot;
        private BattlefieldSpatialSnapshot _snapshot;
        private bool _showTerritory = true;
        private bool _showFrontline = true;
        private bool _showVertexLabels;
        private bool _showGrid = true;
        private bool _showOccupancy = true;

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
            _showGrid = EditorGUILayout.Toggle("Uniform Grid", _showGrid);
            _showOccupancy = EditorGUILayout.Toggle("Cell occupancy", _showOccupancy);

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode에 진입한 뒤 Hierarchy에서 AppRoot를 지정하세요.", MessageType.Warning);
            else if (_appRoot == null || _snapshot == null)
                EditorGUILayout.HelpBox("연결된 AppRoot에 활성 Match snapshot이 없습니다.", MessageType.None);
            else
            {
                EditorGUILayout.LabelField("Match / revision", $"{_snapshot.MatchId} / {_snapshot.Revision}");
                EditorGUILayout.LabelField("Vertices / triangles / frontline", $"{_snapshot.Vertices.Count} / {_snapshot.Triangles.Count} / {_snapshot.FrontlineEdges.Count}");
                DrawGridConfigurationControls();
                EditorGUILayout.LabelField("Out of bounds (T / P / E)",
                    $"{_snapshot.Grid.OutOfBoundsTurretIds.Count} / {_snapshot.Grid.OutOfBoundsPlayerIds.Count} / {_snapshot.Grid.OutOfBoundsEnemyIds.Count}");
            }

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
                if (_showGrid) DrawGrid();
                if (_showTerritory) DrawTerritory(positions);
                if (_showFrontline) DrawFrontlines(positions);
                if (_showVertexLabels) DrawVertexLabels(positions);
                if (_showOccupancy) DrawOccupancy();
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
                SortAroundCentroid(polygon);
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

        private Vector3 ToWorldPosition(BattlefieldPoint point) =>
            new Vector3((float)point.X, (float)point.Y, (float)_snapshot.Grid.Configuration.OriginZ);

        private void DrawGrid()
        {
            var configuration = _snapshot.Grid.Configuration;
            var minX = (float)configuration.Origin.X;
            var minY = (float)configuration.Origin.Y;
            var maxX = minX + (float)configuration.MapWidth;
            var maxY = minY + (float)configuration.MapHeight;
            var z = (float)configuration.OriginZ;
            Handles.color = new Color(0.75f, 0.85f, 1f, 0.55f);
            for (var x = 0; x <= configuration.CellsX; x++)
            {
                var worldX = minX + x * (float)configuration.CellWidth;
                Handles.DrawAAPolyLine(1f, new Vector3(worldX, minY, z), new Vector3(worldX, maxY, z));
            }
            for (var y = 0; y <= configuration.CellsY; y++)
            {
                var worldY = minY + y * (float)configuration.CellHeight;
                Handles.DrawAAPolyLine(1f, new Vector3(minX, worldY, z), new Vector3(maxX, worldY, z));
            }
        }

        private void DrawOccupancy()
        {
            foreach (var cell in _snapshot.Grid.OccupiedCells)
            {
                var configuration = _snapshot.Grid.Configuration;
                var x = configuration.Origin.X + (cell.X + 0.5d) * configuration.CellWidth;
                var y = configuration.Origin.Y + (cell.Y + 0.5d) * configuration.CellHeight;
                var position = new Vector3((float)x, (float)y, (float)configuration.OriginZ);
                Handles.Label(position, $"T:{cell.TurretCount} P:{cell.PlayerCount} E:{cell.EnemyCount}");
            }
        }

        private void DrawGridConfigurationControls()
        {
            var current = _snapshot.Grid.Configuration;
            EditorGUILayout.LabelField("Runtime Grid Configuration", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var originX = EditorGUILayout.DoubleField("Origin X", current.Origin.X);
            var originY = EditorGUILayout.DoubleField("Origin Y", current.Origin.Y);
            var originZ = EditorGUILayout.DoubleField("Origin Z (draw only)", current.OriginZ);
            var mapWidth = EditorGUILayout.DoubleField("Map Width", current.MapWidth);
            var mapHeight = EditorGUILayout.DoubleField("Map Height", current.MapHeight);
            var cellsX = EditorGUILayout.IntField("Cells X", current.CellsX);
            var cellsY = EditorGUILayout.IntField("Cells Y", current.CellsY);
            if (!EditorGUI.EndChangeCheck()) return;

            try
            {
                var configuration = new BattlefieldGridConfiguration(
                    new BattlefieldPoint(originX, originY), originZ, mapWidth, mapHeight, cellsX, cellsY);
                _appRoot.ReconfigureBattlefieldGrid(configuration);
            }
            catch (System.ArgumentException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        private static void SortAroundCentroid(Vector3[] points)
        {
            var center = (points[0] + points[1] + points[2]) / 3f;
            System.Array.Sort(points, (left, right) =>
            {
                var leftAngle = Mathf.Atan2(left.y - center.y, left.x - center.x);
                var rightAngle = Mathf.Atan2(right.y - center.y, right.x - center.x);
                return leftAngle.CompareTo(rightAngle);
            });
        }
    }
}
