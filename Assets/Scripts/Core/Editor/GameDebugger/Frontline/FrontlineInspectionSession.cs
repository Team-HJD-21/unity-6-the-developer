// Scene 전선 선택과 현재 snapshot의 읽기 전용 기하 진단을 공유합니다.
// 변의 정규화된 터렛 ID 쌍으로 revision 간 선택을 유지하며 Match 종료 시 해제합니다.
using TeamHJD.Game.Domain;
using UnityEditor;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class FrontlineInspectionSession
    {
        private static BattlefieldEdge? _selectedEdge;
        private static bool _previousCellPickingEnabled;
        public static bool IsPickingEnabled { get; private set; }
        public static FrontlineGeometryReport Report { get; private set; }
        public static FrontlineEdgeInspection Selected =>
            _selectedEdge.HasValue ? Report?.Find(_selectedEdge.Value) : null;

        static FrontlineInspectionSession()
        {
            BattlefieldDebugSession.Changed += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            Refresh();
        }

        public static void SetPickingEnabled(bool enabled)
        {
            if (IsPickingEnabled == enabled) return;
            IsPickingEnabled = enabled;
            if (enabled)
            {
                _previousCellPickingEnabled = BattlefieldDebugSession.Display.IsCellPickingEnabled;
                BattlefieldDebugSession.Display.IsCellPickingEnabled = false;
                BattlefieldDebugSession.Display.IsEnabled = true;
                BattlefieldDebugSession.Display.ShowFrontline = true;
            }
            else BattlefieldDebugSession.Display.IsCellPickingEnabled = _previousCellPickingEnabled;
            BattlefieldDebugSession.NotifyDisplayChanged();
        }

        public static void Select(BattlefieldEdge? edge)
        {
            Refresh();
            _selectedEdge = edge.HasValue && Report?.Find(edge.Value) != null ? edge : null;
            SceneView.RepaintAll();
        }

        public static void YieldToCellPicking()
        {
            if (!IsPickingEnabled) return;
            // 사용자가 새로 켠 셀 선택을 이전 값으로 덮어쓰지 않습니다.
            IsPickingEnabled = false;
            BattlefieldDebugSession.NotifyDisplayChanged();
        }

        public static void Refresh()
        {
            BattlefieldSpatialSnapshot snapshot = BattlefieldDebugSession.Snapshot;
            if (ReferenceEquals(Report?.Snapshot, snapshot)) return;
            if (snapshot == null || Report != null && Report.Snapshot.MatchId != snapshot.MatchId)
                _selectedEdge = null;
            Report = FrontlineGeometryReport.Create(snapshot,
                (float)(BattlefieldDebugSession.Configuration?.OriginZ ?? 0), Report);
            // 같은 Match여도 파괴로 해당 ID 쌍이 더 이상 경계가 아니면 선택을 버립니다.
            if (_selectedEdge.HasValue && Report?.Find(_selectedEdge.Value) == null) _selectedEdge = null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            _selectedEdge = null;
            Report = null;
            SetPickingEnabled(false);
        }

        private static void Shutdown()
        {
            BattlefieldDebugSession.Changed -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SetPickingEnabled(false);
        }
    }
}
