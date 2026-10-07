// 창 유무와 관계없이 Editor의 공간 진단 소스와 표시 상태를 유지합니다.
// LIVE는 현재 Match, PREVIEW는 Core 기하 계산 결과입니다. 오래된 Match는 보관하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class BattlefieldDebugSession
    {
        private static readonly List<string> _history = new List<string>();
        private static readonly string PreferenceKey = "TeamHJD.Battlefield.Display." + Application.dataPath;
        private static double _nextRefresh;
        private static BattlefieldGridConfiguration _previewConfiguration;
        private static int _sceneHandle;
        private static int _selectedCellId = -1;

        public static event Action Changed;
        public static AppRoot AppRoot { get; private set; }
        public static BattlefieldSpatialSnapshot Snapshot { get; private set; }
        public static BattlefieldGridSnapshot Grid { get; private set; }
        public static BattlefieldGridSettings GridSettings { get; set; }
        public static string Error { get; set; }
        public static BattlefieldDebugDisplaySettings Display { get; }
        public static IReadOnlyList<string> History => _history.AsReadOnly();
        public static string Mode => EditorApplication.isPlaying
            ? (Snapshot != null && AppRoot != null ? "LIVE" : "UNAVAILABLE") : "PREVIEW";
        public static BattlefieldGridConfiguration Configuration => Grid?.Configuration ?? _previewConfiguration;
        public static int SelectedCellId => _selectedCellId;

        static BattlefieldDebugSession()
        {
            Display = new BattlefieldDebugDisplaySettings();
            string saved = EditorPrefs.GetString(PreferenceKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                try { JsonUtility.FromJsonOverwrite(saved, Display); }
                catch (ArgumentException) { EditorPrefs.DeleteKey(PreferenceKey); }
            }
            GridSettings = AssetDatabase.LoadAssetAtPath<BattlefieldGridSettings>(
                "Assets/PoC/Core/Content/Authoring/BattlefieldGridSettings_Default.asset");
            EditorApplication.update += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyChanged += InvalidatePreview;
            Undo.undoRedoPerformed += InvalidatePreview;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }

        public static void Bind(AppRoot root)
        {
            if (AppRoot != null) AppRoot.BattlefieldSnapshotChanged -= OnSnapshotChanged;
            AppRoot = root;
            if (AppRoot != null) AppRoot.BattlefieldSnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(AppRoot == null ? null : AppRoot.CurrentBattlefieldSnapshot);
        }

        public static void NotifyDisplayChanged()
        {
            EditorPrefs.SetString(PreferenceKey, JsonUtility.ToJson(Display));
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        public static void SelectCell(int cellId)
        {
            _selectedCellId = Grid != null && cellId >= 0 && cellId < Grid.Cells.Count ? cellId : -1;
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        public static void InvalidatePreview() => _nextRefresh = 0d;

        private static void Refresh()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25d;
            int sceneHandle = SceneManager.GetActiveScene().handle;
            if (_sceneHandle != sceneHandle)
            {
                _sceneHandle = sceneHandle;
                _history.Clear();
                _selectedCellId = -1;
                Snapshot = null;
                Grid = null;
            }
            if (EditorApplication.isPlaying)
            {
                _previewConfiguration = null;
                if (AppRoot == null) Bind(UnityEngine.Object.FindAnyObjectByType<AppRoot>());
                else if (!ReferenceEquals(Snapshot, AppRoot.CurrentBattlefieldSnapshot))
                    OnSnapshotChanged(AppRoot.CurrentBattlefieldSnapshot);
                return;
            }
            // 표시/창을 사용하지 않는 프로젝트에서는 반복적인 Scene 검색을 하지 않습니다.
            if (!Display.IsEnabled && Changed == null) return;
            try
            {
                BattlefieldGridConfiguration configuration = GridSettings != null
                    ? GridSettings.CreateConfiguration() : BattlefieldGridConfiguration.Default;
                IBattlefieldTurretInputSource source = FindPreviewSource();
                BattlefieldSpatialInput input = source?.CaptureTurretLayout() ?? BattlefieldSpatialInput.Empty;
                if (Snapshot != null && SameInput(Snapshot, input) && SameConfiguration(_previewConfiguration, configuration)) return;
                Snapshot = new BattlefieldTopologyBuilder().Build(input);
                Grid = new BattlefieldGridBuilder().Build(configuration, input, BattlefieldDynamicSpatialInput.Empty);
                _previewConfiguration = configuration;
                _selectedCellId = -1;
                Error = null;
                Publish("PREVIEW geometry updated");
            }
            catch (Exception exception)
            {
                Snapshot = null;
                Grid = null;
                _previewConfiguration = null;
                if (Error == exception.Message) return;
                Error = exception.Message;
                Publish("PREVIEW unavailable: " + Error);
            }
        }

        private static IBattlefieldTurretInputSource FindPreviewSource()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null && component.isActiveAndEnabled &&
                        component is IBattlefieldTurretInputSource source) return source;
            return null;
        }

        private static bool SameInput(BattlefieldSpatialSnapshot snapshot, BattlefieldSpatialInput input)
        {
            if (snapshot.Vertices.Count != input.Turrets.Count) return false;
            var positions = new Dictionary<EntityId, BattlefieldPoint>();
            foreach (BattlefieldVertex vertex in snapshot.Vertices) positions[vertex.EntityId] = vertex.Position;
            foreach (TurretSpatialInput turret in input.Turrets)
                if (!positions.TryGetValue(turret.EntityId, out BattlefieldPoint position) || position != turret.Position) return false;
            return true;
        }

        private static bool SameConfiguration(BattlefieldGridConfiguration left, BattlefieldGridConfiguration right) =>
            left != null && right != null && left.Origin == right.Origin && left.OriginZ == right.OriginZ &&
            left.MapWidth == right.MapWidth && left.MapHeight == right.MapHeight &&
            left.CellsX == right.CellsX && left.CellsY == right.CellsY;

        private static void OnSnapshotChanged(BattlefieldSpatialSnapshot snapshot)
        {
            bool sourceChanged = snapshot == null || Snapshot == null ||
                Snapshot.MatchId != snapshot.MatchId || !SameConfiguration(Grid?.Configuration, snapshot.Grid.Configuration);
            Snapshot = snapshot;
            Grid = snapshot?.Grid;
            Error = null;
            if (sourceChanged) _selectedCellId = -1;
            if (snapshot == null) _previewConfiguration = null;
            Publish(snapshot == null ? "Match detached" : $"LIVE {snapshot.MatchId} r{snapshot.Revision} | V/T/F " +
                $"{snapshot.Vertices.Count}/{snapshot.Triangles.Count}/{snapshot.FrontlineEdges.Count} | occupied {snapshot.Grid.OccupiedCells.Count}");
        }

        private static void Publish(string message)
        {
            if (_history.Count >= 64) _history.RemoveAt(0);
            _history.Add($"{DateTime.Now:HH:mm:ss} {message}");
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Bind(null);
                _history.Clear();
            }
            InvalidatePreview();
        }

        private static void Shutdown()
        {
            if (AppRoot != null) AppRoot.BattlefieldSnapshotChanged -= OnSnapshotChanged;
            EditorApplication.update -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.hierarchyChanged -= InvalidatePreview;
            Undo.undoRedoPerformed -= InvalidatePreview;
            Snapshot = null;
            Grid = null;
        }
    }
}
