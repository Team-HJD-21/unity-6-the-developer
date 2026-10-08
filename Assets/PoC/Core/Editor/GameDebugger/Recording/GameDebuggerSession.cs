// 창과 독립적으로 Feature 이벤트와 읽기 전용 표본을 수집합니다.
// 요청 실패·터렛 파괴·범위 밖 조건은 사용자가 무장한 경우에만 로컬 Editor를 정지합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Application = UnityEngine.Application;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class GameDebuggerSession
    {
        private static IGameDiagnosticsSource _source;
        private static double _nextSample, _nextSearch;
        private static double _nextValidation;
        private static BattlefieldSpatialSnapshot _geometrySnapshot;
        private static double _area, _length;
        private static long _revision = -1;
        private static bool _pauseLatched;
        private static readonly string ArchiveKey = "TeamHJD.GameDebugger.Archive." + Application.dataPath;
        private static double _snapshotObservedAt;
        private static readonly List<string> _warnings = new List<string>();
        public static GameDebugRecorder Recorder { get; } = new GameDebugRecorder();
        public static event Action Changed;
        public static IGameDiagnosticsSource Source => IsSourceAlive() ? _source : null;
        public static GameDebugRun Current => Source == null ? null : Recorder.Find(Source.BoundMatchId.ToString());
        public static GameDebugRun Imported { get; private set; }
        public static IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
        public static bool ShouldPauseOnFailure, ShouldPauseOnTurretDestroyed, ShouldPauseOnOutOfBounds;
        public static string LastPauseReason { get; private set; }
        public static double SnapshotObservedAge => _revision < 0 ? -1 : Math.Max(0,EditorApplication.timeSinceStartup-_snapshotObservedAt);

        static GameDebuggerSession()
        {
            RestoreArchives();
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += SaveAndDetach;
            if (EditorApplication.isPlaying) SubscribeTurrets();
        }

        private static bool IsSourceAlive() => _source is MonoBehaviour component && component != null && component.isActiveAndEnabled;
        private static void RestoreArchives()
        {
            string json=SessionState.GetString(ArchiveKey,"");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var archive=JsonUtility.FromJson<GameDebugArchive>(json);
                if (archive?.Runs == null || archive.Runs.Count>GameDebugRecorder.MaximumRuns) return;
                foreach (var run in archive.Runs)
                {
                    var validated=GameDebugExport.FromJson(GameDebugExport.ToJson(run));
                    if (!validated.IsComplete) validated.IsPartial=true;
                    Recorder.RestoreArchives(new[]{validated});
                }
            }
            catch (ArgumentException) { SessionState.EraseString(ArchiveKey); }
        }
        private static void SaveAndDetach()
        {
            var archive=new GameDebugArchive { Runs=Recorder.Runs.ToList() };
            string json=JsonUtility.ToJson(archive);
            // SessionState는 한 Editor 세션의 메모리 저장입니다. 자동 파일 생성은 하지 않습니다.
            while (json.Length>8*1024*1024 && archive.Runs.Count>0)
            { archive.Runs.RemoveAt(0); json=JsonUtility.ToJson(archive); }
            SessionState.SetString(ArchiveKey,json);
            Detach();
        }
        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            { Detach(); SubscribeTurrets(); Rearm(); _revision = -1; _nextSearch = _nextValidation = 0; _geometrySnapshot = null; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // MatchEnded를 받지 못한 실행을 정상 종료로 꾸미지 않습니다.
                foreach (var run in Recorder.Runs) if (!run.IsComplete) run.IsPartial = true;
                Detach(); Changed?.Invoke();
            }
        }
        private static void SubscribeTurrets()
        {
            TurretInstanceRegistry.Destroyed -= OnTurretDestroyed;
            TurretInstanceRegistry.Restored -= OnTurretRestored;
            TurretInstanceRegistry.Destroyed += OnTurretDestroyed;
            TurretInstanceRegistry.Restored += OnTurretRestored;
        }
        private static void Detach()
        {
            if (_source != null) _source.DiagnosticEmitted -= OnDiagnostic;
            _source = null;
            TurretInstanceRegistry.Destroyed -= OnTurretDestroyed;
            TurretInstanceRegistry.Restored -= OnTurretRestored;
        }
        private static void Update()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            double now = EditorApplication.timeSinceStartup;
            if (!IsSourceAlive() && now >= _nextSearch)
            {
                _nextSearch = now + 1;
                if (_source != null) _source.DiagnosticEmitted -= OnDiagnostic;
                var candidates = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Where(item => item != null && item.isActiveAndEnabled).OfType<IGameDiagnosticsSource>().ToArray();
                _source = candidates.Length == 1 ? candidates[0] : null;
                if (_source != null)
                {
                    _source.DiagnosticEmitted += OnDiagnostic;
                    foreach (var item in _source.RecentDiagnostics) Accept(item, true);
                }
                else
                {
                    _warnings.Clear();
                    _warnings.Add($"진단 소스 {candidates.Length}개: 단일 Match 소스를 명확히 지정해야 합니다.");
                }
            }
            if (!IsSourceAlive() || now < _nextSample) return;
            _nextSample = now + 0.25;
            var run = Current;
            if (run == null) return;
            run.RuntimeDroppedEvents = _source.DroppedDiagnosticCount;
            Sample(run);
            Changed?.Invoke();
        }
        private static GameDebugRun CreateRun(GameDiagnosticEvent item)
        {
            var root = BattlefieldDebugSession.AppRoot ?? UnityEngine.Object.FindAnyObjectByType<TeamHJD.Game.Bootstrap.AppRoot>();
            var config = root == null ? null : root.CurrentMatchConfig;
            bool matches = config != null && config.MatchId == item.MatchId;
            return new GameDebugRun {
                Scene = SceneManager.GetActiveScene().path, UnityVersion = Application.unityVersion,
                BuildLabel = BattlefieldDebugSession.Display.CaptureLabel, StartedAt = DateTime.Now.ToString("O"),
                ContentVersion = matches ? config.ContentVersion : "Unknown", RandomSeed = matches ? config.RandomSeed : 0,
                StageId = matches ? config.StageId.ToString() : "Unknown", DifficultyProfileId = matches ? config.DifficultyProfileId.ToString() : "Unknown",
                MatchMode = matches ? config.Mode.ToString() : "Unknown",
                HasSeed = matches, GridDescription = !matches || root.CurrentBattlefieldSnapshot == null ? "Unknown" :
                    JsonUtility.ToJson(new GridMetadata(root.CurrentBattlefieldSnapshot.Grid.Configuration))
            };
        }
        private static void OnDiagnostic(GameDiagnosticEvent item) => Accept(item, false);
        private static void Accept(GameDiagnosticEvent item, bool replay)
        {
            Recorder.Accept(item, () => CreateRun(item));
            if (!replay && item.Kind == GameDiagnosticKind.RequestFailed && ShouldPauseOnFailure) Pause("Encounter 요청 실패: " + item.Message);
            Changed?.Invoke();
        }
        private static void Sample(GameDebugRun run)
        {
            var snapshot = BattlefieldDebugSession.Snapshot;
            if (snapshot == null || snapshot.MatchId.ToString() != run.MatchId) return;
            var turrets = GetSceneTurrets();
            var controllers = UnityEngine.Object.FindObjectsByType<TurretController>(FindObjectsSortMode.None)
                .Where(item => item.isActiveAndEnabled && item.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
            // GetSnapshot는 controller의 보류 전력까지 분리한 공식 조회 경로입니다.
            var budgets = controllers.Select(item => item.GetSnapshot()).ToArray();
            if (_geometrySnapshot == null || !ReferenceEquals(_geometrySnapshot.Vertices,snapshot.Vertices))
            { CalculateGeometry(snapshot,out _area,out _length); _geometrySnapshot=snapshot; }
            Recorder.AddSample(run, new GameDebugSample {
                GameSeconds = _source.GameSeconds, RealSeconds = _source.RealSeconds, Revision = snapshot.Revision,
                AliveEnemies = _source.IsServer ? _source.ActiveEnemyCount : -1,
                UnattributedEnemies = _source.IsServer ? _source.UnattributedEnemyCount : -1,
                TurretCount = turrets.Count, ActivatedTurrets = turrets.Count(item => item.IsActivated),
                OperationalTurrets = turrets.Count(item => item.IsOperational),
                AvailablePower = budgets.Length == 0 ? -1 : budgets.Sum(item => item.AvailablePower),
                MaximumPower = budgets.Length == 0 ? -1 : budgets.Sum(item => item.MaximumPower),
                TimeScale = Time.timeScale, TerritoryArea = _area, FrontlineLength = _length
            });
            if (_revision != snapshot.Revision)
            { _revision = snapshot.Revision; _snapshotObservedAt=EditorApplication.timeSinceStartup; AddMarker($"Snapshot r{_revision}: triangles {snapshot.Triangles.Count}, frontline {snapshot.FrontlineEdges.Count}"); }
            var grid = snapshot.Grid;
            if (run.GridDescription == "Unknown") run.GridDescription=JsonUtility.ToJson(new GridMetadata(grid.Configuration));
            bool oob = grid.OutOfBoundsTurretIds.Count + grid.OutOfBoundsEnemyIds.Count + grid.OutOfBoundsPlayerIds.Count > 0;
            if (oob && ShouldPauseOnOutOfBounds) Pause("Grid 범위 밖 입력");
            if (EditorApplication.timeSinceStartup<_nextValidation) return;
            _nextValidation=EditorApplication.timeSinceStartup+1;
            _warnings.Clear();
            if (snapshot.Triangles.Count == 0) _warnings.Add("Territory 없음: 비공선인 활성 터렛 3개 이상을 확인하세요.");
            if (!_source.IsServer) _warnings.Add("서버 실제 생성 수/생존 수 계약 없음: Client에서 서버 수를 추측하지 않습니다.");
            if (_source.UnattributedEnemyCount > 0) _warnings.Add($"현재 Match에 귀속되지 않은 Executor Enemy: {_source.UnattributedEnemyCount}");
            if (oob) _warnings.Add("Grid 범위 밖 입력이 있습니다.");
            ValidateSpawnPoints(grid);
        }
        public static IReadOnlyList<TurretSnapshot> GetSceneTurrets()
        {
            var scene = SceneManager.GetActiveScene();
            return TurretInstanceRegistry.GetAllSnapshots().Where(item =>
                TurretInstanceRegistry.TryGet(item.InstanceId, out var turret) && turret != null && turret.gameObject.scene == scene).ToArray();
        }
        private static void ValidateSpawnPoints(BattlefieldGridSnapshot grid)
        {
            // Assembly-CSharp를 참조할 수 없는 Editor assembly의 Feature 경계입니다.
            // 생성 정책을 재실행하지 않고 공개 ID/위치 계약만 검사합니다.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var component in components)
            {
                if (component == null || !component.isActiveAndEnabled || component.gameObject.scene != SceneManager.GetActiveScene() || component.GetType().Name != "SpawnPoint") continue;
                count++;
                string id = component.GetType().GetProperty("SpawnPointId")?.GetValue(component) as string;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) _warnings.Add("SpawnPoint ID 누락/중복: " + component.name);
                if (!grid.Configuration.TryMapPoint(new BattlefieldPoint(component.transform.position.x, component.transform.position.y), out _, out _))
                    _warnings.Add("SpawnPoint가 Grid 밖에 있습니다: " + component.name);
            }
            if (count == 0) _warnings.Add("활성 Scene SpawnPoint가 없습니다.");
            if (!components
                .Any(item => item != null && item.isActiveAndEnabled && item.gameObject.scene == SceneManager.GetActiveScene() && item.GetType().Name == "TargetableComponent"))
                _warnings.Add("Scene TargetableComponent 없음. 공격/피해 계약의 실제 연결 여부는 Feature 테스트가 필요합니다.");
        }
        public static void CalculateGeometry(BattlefieldSpatialSnapshot snapshot, out double area, out double length)
        {
            area = length = 0;
            var positions = snapshot.Vertices.ToDictionary(item => item.EntityId, item => item.Position);
            foreach (var triangle in snapshot.Triangles)
            {
                var a=positions[triangle.VertexA]; var b=positions[triangle.VertexB]; var c=positions[triangle.VertexC];
                area += Math.Abs((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))*0.5;
            }
            foreach (var edge in snapshot.FrontlineEdges)
            {
                var a=positions[edge.First]; var b=positions[edge.Second];
                length += Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
            }
        }
        public static void AddMarker(string message, int turretId = 0)
        {
            var run = Current;
            if (run == null || run.IsComplete) return;
            if (run.Markers.Count >= 512) { run.Markers.RemoveAt(0); run.IsPartial = true; }
            run.Markers.Add(new GameDebugMarker { GameSeconds = _source.GameSeconds, RealSeconds = _source.RealSeconds, Message = message, TurretId = turretId });
            Changed?.Invoke();
        }
        private static void OnTurretDestroyed(TurretBase turret)
        {
            if (turret.gameObject.scene != SceneManager.GetActiveScene()) return;
            AddMarker("Turret destroyed: " + turret.name, turret.InstanceId);
            if (ShouldPauseOnTurretDestroyed) Pause("터렛 파괴: " + turret.name);
        }
        private static void OnTurretRestored(TurretBase turret)
        {
            if (turret.gameObject.scene == SceneManager.GetActiveScene()) AddMarker("Turret restored: " + turret.name, turret.InstanceId);
        }
        private static void Pause(string reason)
        {
            if (_pauseLatched) return;
            _pauseLatched = true; LastPauseReason = reason;
            AddMarker("Conditional pause: " + reason); GameSimulationControls.Pause();
        }
        public static void Rearm() { _pauseLatched = false; LastPauseReason = null; }
        public static void SetImported(GameDebugRun run) { Imported = run; Changed?.Invoke(); }
    }
}
