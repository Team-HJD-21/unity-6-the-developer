// 공용 Battlefield Grid 기본값을 편집하고 활성 Match의 공간 데이터를 진단합니다.
// Play Mode 오버라이드는 현재 Match에만 적용하며 authoring asset은 변경하지 않습니다.

using System.Collections.Generic;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
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
        [SerializeField] private BattlefieldGridSettings _gridSettings;
        private BattlefieldGridConfiguration _runtimeConfiguration;
        private MatchId _runtimeMatchId;
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
            if (_gridSettings == null)
                _gridSettings = AssetDatabase.LoadAssetAtPath<BattlefieldGridSettings>(
                    "Assets/PoC/Core/Content/Authoring/BattlefieldGridSettings_Default.asset");
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
            DrawAuthoredGridSettings();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Match Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode의 실행 중 Match snapshot을 진단합니다. Edit Mode fixture는 생성하지 않습니다.", MessageType.Info);
            EditorGUILayout.HelpBox("Scene View Grid는 Edit Mode에서 공용 기본값, Play Mode에서 활성 Match 설정을 표시합니다.", MessageType.Info);
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
                DrawRuntimeGridControls();
                EditorGUILayout.LabelField("Out of bounds (T / P / E)",
                    $"{_snapshot.Grid.OutOfBoundsTurretIds.Count} / {_snapshot.Grid.OutOfBoundsPlayerIds.Count} / {_snapshot.Grid.OutOfBoundsEnemyIds.Count}");
            }

            SceneView.RepaintAll();
        }

        private void DrawAuthoredGridSettings()
        {
            EditorGUILayout.LabelField("Authored Grid Defaults", EditorStyles.boldLabel);
            _gridSettings = (BattlefieldGridSettings)EditorGUILayout.ObjectField(
                "Shared Settings Asset", _gridSettings, typeof(BattlefieldGridSettings), false);
            if (_gridSettings == null)
            {
                EditorGUILayout.HelpBox("공용 Grid 기본값 에셋을 지정하세요. Scene composition은 이 값을 새 Match에 적용합니다.", MessageType.Warning);
                return;
            }

            var serializedSettings = new SerializedObject(_gridSettings);
            serializedSettings.Update();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originX"), new GUIContent("Center X"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originY"), new GUIContent("Center Y"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originZ"), new GUIContent("Origin Z (draw only)"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_mapWidth"), new GUIContent("Map Width"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_mapHeight"), new GUIContent("Map Height"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_cellsX"), new GUIContent("Cells X"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_cellsY"), new GUIContent("Cells Y"));
            serializedSettings.ApplyModifiedProperties();
            try
            {
                _gridSettings.CreateConfiguration();
            }
            catch (System.ArgumentException exception)
            {
                EditorGUILayout.HelpBox($"저장된 설정이 유효하지 않습니다: {exception.Message}", MessageType.Error);
            }

            EditorGUILayout.HelpBox("이 값은 저장되어 이후 생성되는 Match의 기본값이 됩니다. 활성 Match에는 자동 적용되지 않습니다.", MessageType.Info);
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
                ResetRuntimeConfiguration();
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSnapshotChanged(BattlefieldSpatialSnapshot snapshot)
        {
            _snapshot = snapshot;
            if (snapshot == null || snapshot.MatchId != _runtimeMatchId)
                ResetRuntimeConfiguration();
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
            {
                _snapshot = _appRoot.CurrentBattlefieldSnapshot;
                ResetRuntimeConfiguration();
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawSceneView(SceneView sceneView)
        {
            BattlefieldGridConfiguration gridConfiguration = ResolveGridConfiguration();
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                if (_showGrid && gridConfiguration != null) DrawGrid(gridConfiguration);
                if (!EditorApplication.isPlaying || _appRoot == null || _snapshot == null) return;

                var positions = new Dictionary<DomainEntityId, Vector3>(_snapshot.Vertices.Count);
                foreach (var vertex in _snapshot.Vertices)
                    positions.Add(vertex.EntityId, ToWorldPosition(vertex.Position));

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

        private BattlefieldGridConfiguration ResolveGridConfiguration()
        {
            if (EditorApplication.isPlaying && _snapshot != null)
                return _snapshot.Grid.Configuration;

            if (_gridSettings == null) return BattlefieldGridConfiguration.Default;
            try
            {
                return _gridSettings.CreateConfiguration();
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        private static void DrawGrid(BattlefieldGridConfiguration configuration)
        {
            var minX = (float)configuration.MinimumX;
            var minY = (float)configuration.MinimumY;
            var maxX = (float)configuration.MaximumX;
            var maxY = (float)configuration.MaximumY;
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
                var x = configuration.MinimumX + (cell.X + 0.5d) * configuration.CellWidth;
                var y = configuration.MinimumY + (cell.Y + 0.5d) * configuration.CellHeight;
                var position = new Vector3((float)x, (float)y, (float)configuration.OriginZ);
                Handles.Label(position, $"T:{cell.TurretCount} P:{cell.PlayerCount} E:{cell.EnemyCount}");
            }
        }

        private void ResetRuntimeConfiguration()
        {
            _runtimeMatchId = _snapshot == null ? default : _snapshot.MatchId;
            _runtimeConfiguration = _snapshot?.Grid.Configuration;
        }

        private void DrawRuntimeGridControls()
        {
            EditorGUILayout.LabelField("Runtime Grid Override (active Match only)", EditorStyles.boldLabel);
            if (_runtimeConfiguration == null)
            {
                EditorGUILayout.HelpBox("현재 Match의 Grid 설정을 읽을 수 없습니다.", MessageType.Warning);
                return;
            }

            var originX = EditorGUILayout.DoubleField("Center X", _runtimeConfiguration.Origin.X);
            var originY = EditorGUILayout.DoubleField("Center Y", _runtimeConfiguration.Origin.Y);
            var originZ = EditorGUILayout.DoubleField("Origin Z (draw only)", _runtimeConfiguration.OriginZ);
            var mapWidth = EditorGUILayout.DoubleField("Map Width", _runtimeConfiguration.MapWidth);
            var mapHeight = EditorGUILayout.DoubleField("Map Height", _runtimeConfiguration.MapHeight);
            var cellsX = EditorGUILayout.IntField("Cells X", _runtimeConfiguration.CellsX);
            var cellsY = EditorGUILayout.IntField("Cells Y", _runtimeConfiguration.CellsY);

            try
            {
                _runtimeConfiguration = new BattlefieldGridConfiguration(
                    new BattlefieldPoint(originX, originY), originZ, mapWidth, mapHeight, cellsX, cellsY);
            }
            catch (System.ArgumentException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to Active Match"))
                _appRoot.ReconfigureBattlefieldGrid(_runtimeConfiguration);
            if (GUILayout.Button("Restore Authored Defaults"))
            {
                if (_gridSettings == null)
                    EditorGUILayout.HelpBox("먼저 공용 Grid 설정 에셋을 지정하세요.", MessageType.Warning);
                else
                {
                    try
                    {
                        _runtimeConfiguration = _gridSettings.CreateConfiguration();
                        _appRoot.ReconfigureBattlefieldGrid(_runtimeConfiguration);
                    }
                    catch (System.ArgumentException exception)
                    {
                        EditorGUILayout.HelpBox($"저장된 설정이 유효하지 않습니다: {exception.Message}", MessageType.Error);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("여기서 적용한 값은 현재 Match에서만 유지됩니다. 공유 설정 에셋에 저장되지 않습니다.", MessageType.Warning);
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
