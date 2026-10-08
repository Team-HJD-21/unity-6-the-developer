// Game Debugger의 연속 Encounter 요청 UI와 Editor update 수명을 관리합니다.
// 모든 pane이 단일 실행기를 공유하며 시작한 pane의 해제·Match 종료·reload 시 자동 중지합니다.
// 실제 생성은 현재 Match를 조회하는 공통 Encounter 계약만 호출합니다.
using System;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class EncounterRepeatDebugControls
    {
        private static readonly EncounterRepeatScheduler _scheduler = new EncounterRepeatScheduler();
        private static object _owner;
        private static IEncounterDebugCommandTarget _target;
        private static string _squadOrder;
        private static int _maxEnemyCount;
        private static double _intervalSeconds = 2;
        private static int _maximumRequests = 10;
        private static bool _isRealtime;
        private static bool _isExpanded;
        private static bool _areDetailsExpanded;
        private static Vector2 _detailsScroll;
        private static double _previousRealTime, _previousGameTime;
        public static EncounterRepeatScheduler Scheduler => _scheduler;

        static EncounterRepeatDebugControls()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.pauseStateChanged += OnPauseStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        public static void Draw(object owner, string squadOrder, int maxEnemyCount, IEncounterDebugCommandTarget target)
        {
            string title = "연속 Encounter 요청 / 시뮬레이션";
            if (_scheduler.IsRunning) title += EditorApplication.isPaused || Time.timeScale <= 0 ? " [PAUSED]" : " [RUNNING]";
            _isExpanded = EditorGUILayout.Foldout(_isExpanded, title, true);
            if (!_isExpanded) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledGroupScope(_scheduler.IsRunning))
                {
                    double entered = EditorGUILayout.DoubleField("요청 간격 (초)", _intervalSeconds);
                    _intervalSeconds = double.IsNaN(entered) || double.IsInfinity(entered)
                        ? 2 : Math.Max(EncounterRepeatScheduler.MinimumIntervalSeconds, entered);
                    _maximumRequests = Mathf.Clamp(EditorGUILayout.IntField("최대 요청 횟수 (0 = 무제한)", _maximumRequests),
                        0, EncounterRepeatScheduler.MaximumFiniteRequests);
                    _isRealtime = EditorGUILayout.Toggle("실시간 clock 사용", _isRealtime);
                }
                string matchId = CurrentMatchId();
                string readinessReason = null;
                bool ready = IsReady(target, matchId, out readinessReason);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledGroupScope(_scheduler.IsRunning || !EditorApplication.isPlaying ||
                        EditorApplication.isPaused || Time.timeScale <= 0 || string.IsNullOrEmpty(matchId) ||
                        !ready || string.IsNullOrWhiteSpace(squadOrder) || maxEnemyCount < 1))
                        if (GUILayout.Button("Start repeat")) Start(owner, target, matchId, squadOrder, maxEnemyCount);
                    using (new EditorGUI.DisabledGroupScope(!_scheduler.IsRunning))
                        if (GUILayout.Button("Stop repeat")) Stop("사용자가 중지했습니다.");
                }
                EditorGUILayout.LabelField("상태", _scheduler.IsRunning
                    ? (EditorApplication.isPaused || Time.timeScale <= 0 ? "PAUSED" : "RUNNING") : "STOPPED", EditorStyles.boldLabel);
                if (_scheduler.IsRunning)
                {
                    EditorGUILayout.LabelField("다음 요청까지", $"{_scheduler.RemainingSeconds:0.0} 초 ({(_isRealtime ? "실시간" : "게임시간")})");
                    EditorGUILayout.LabelField("고정 요청 설정", $"{_squadOrder} / 요청당 최대 {_maxEnemyCount} 마리");
                }
                EditorGUILayout.LabelField("성공 / 시도", $"{_scheduler.CompletedRequests} / {_scheduler.AttemptedRequests}" +
                    (_scheduler.MaximumRequests > 0 ? $" (상한 {_scheduler.MaximumRequests}회)" : " (무제한)"));
                if (!string.IsNullOrEmpty(_scheduler.StopReason))
                    EditorGUILayout.HelpBox(_scheduler.StopReason, _scheduler.AttemptedRequests > _scheduler.CompletedRequests
                        || (!_scheduler.IsRunning && _scheduler.StopReason != "지정한 요청 횟수를 완료했습니다.")
                        ? MessageType.Warning : MessageType.Info);
                if (_maximumRequests == 0) EditorGUILayout.HelpBox("무제한 요청은 생존 적을 누적할 수 있습니다. 요청당 예산은 생존 적 전체의 상한이 아닙니다.", MessageType.Warning);
                _areDetailsExpanded = EditorGUILayout.Foldout(_areDetailsExpanded, "마지막 실행 결과 / 시간·중지 정책", true);
                if (_areDetailsExpanded)
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_detailsScroll, GUILayout.Height(130)))
                    {
                        _detailsScroll = scroll.scrollPosition;
                        EditorGUILayout.LabelField(_scheduler.LastResult ?? "결과 없음", EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("기본 게임시간은 배속을 따릅니다. 두 clock 모두 Editor Pause·timeScale 0에서 대기하며 재개 시 몰아서 요청하지 않습니다. " +
                            "요청은 최신 Match snapshot을 사용합니다. 실패·Match 변경·시작 pane 해제 시 중지합니다.", EditorStyles.wordWrappedLabel);
                    }
                }
                if (!ready && !string.IsNullOrEmpty(readinessReason)) EditorGUILayout.HelpBox(readinessReason, MessageType.Warning);
            }
        }

        /// <summary>시작한 pane이 닫히거나 재구성될 때만 해당 실행을 중지합니다.</summary>
        public static void Release(object owner)
        {
            if (ReferenceEquals(_owner, owner)) Stop("요청을 시작한 pane이 닫히거나 재구성되어 중지했습니다.");
        }

        private static void Start(object owner, IEncounterDebugCommandTarget target, string matchId, string squadOrder, int maxEnemyCount)
        {
            if (!_scheduler.Start(matchId, _intervalSeconds, _maximumRequests)) return;
            _owner = owner;
            _target = target;
            _squadOrder = squadOrder;
            _maxEnemyCount = maxEnemyCount;
            ResetClocks();
        }

        private static void Update()
        {
            if (!_scheduler.IsRunning) return;
            double nowReal = EditorApplication.timeSinceStartup;
            double nowGame = Time.timeAsDouble;
            double delta = _isRealtime ? nowReal - _previousRealTime : nowGame - _previousGameTime;
            _previousRealTime = nowReal;
            _previousGameTime = nowGame;
            // 느린 Editor 프레임의 realtime 지연도 한 번 요청으로 끝내고 다음 간격부터 다시 기다립니다.
            bool ready = IsReady(_target, _scheduler.MatchId, out string reason);
            _scheduler.Tick(delta, CurrentMatchId(), EditorApplication.isPlaying,
                EditorApplication.isPaused || Time.timeScale <= 0, ready, reason, RequestCurrent);
            if (!_scheduler.IsRunning) ClearReferences();
        }

        private static bool RequestCurrent(out string result) => _target.TryRequestCurrentEncounter(
            _squadOrder, _maxEnemyCount, EncounterRequestSource.DebugWindow, out result);

        private static bool IsReady(IEncounterDebugCommandTarget target, string matchId, out string reason)
        {
            if (target == null || (target is UnityEngine.Object component && component == null) ||
                (target is MonoBehaviour behaviour && !behaviour.isActiveAndEnabled))
            { reason = "Encounter 실행 대상이 제거되었거나 비활성화되었습니다."; return false; }
            // 현재 구현의 진단 계약이 제공하는 Match ID도 확인해 다른 provider로의 우발적 요청을 막습니다.
            if (target is IGameDiagnosticsSource diagnostics && diagnostics.BoundMatchId.ToString() != matchId)
            { reason = "Encounter 대상과 활성 Match가 다릅니다."; return false; }
            try { return target.CanRequestEncounter(out reason); }
            catch (Exception exception)
            { reason = "Encounter 준비 상태 확인 실패: " + exception.Message; return false; }
        }

        private static string CurrentMatchId()
        {
            var snapshot = BattlefieldDebugSession.AppRoot != null
                ? BattlefieldDebugSession.AppRoot.CurrentBattlefieldSnapshot : null;
            return snapshot == null || snapshot.MatchId.IsEmpty ? null : snapshot.MatchId.ToString();
        }

        private static void ResetClocks()
        {
            _previousRealTime = EditorApplication.timeSinceStartup;
            _previousGameTime = Time.timeAsDouble;
        }

        private static void Stop(string reason) { _scheduler.Stop(reason); ClearReferences(); }
        private static void ClearReferences() { _owner = null; _target = null; }
        private static void BeforeReload() => Stop("스크립트 reload로 중지했습니다.");
        private static void OnEditorQuitting() => Stop("Editor 종료로 중지했습니다.");
        private static void OnPauseStateChanged(PauseState state) => ResetClocks();
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) Stop("Play Mode 전환으로 중지했습니다.");
            ResetClocks();
        }
    }
}
