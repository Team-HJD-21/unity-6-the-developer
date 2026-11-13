// Game Debugger에서 지역 정책을 선택하고 확정 Grid와 임시 판정 자료를 구분해 표시합니다.
// 실제 변경은 Match 바인딩에 요청하며 Editor가 별도 지역 상태를 만들지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class RegionDebugControls
    {
        public static bool DrawCapturedCells = true;
        public static bool DrawInputTriangles;
        private static bool _showStartCells;
        private static bool _showCaptureSettings;
        private static Vector2 _captureScroll;
        private static readonly List<Vector2Int> _startDraft = new List<Vector2Int>();
        private static Vector2 _startScroll;
        private static Guid _draftGrid;
        private static RegionSceneBinding _draftBinding;
        private static string _startError;
        private static int _startPage;
        public static RegionSceneBinding FindBinding()
        {
            var bindings = UnityEngine.Object.FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None)
                .Where(binding => binding.isActiveAndEnabled && binding.Source != null).ToArray();
            if (bindings.Length != 1) return null;
            return bindings[0];
        }

        public static void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("지역 Grid · 점령 원본", EditorStyles.boldLabel);
                RegionSceneBinding binding = FindBinding();
                if (binding == null)
                {
                    EditorGUILayout.HelpBox("지역 바인딩이 있는 Match를 시작하세요. 기본 정책은 Static입니다.", MessageType.Info);
                    if (!EditorApplication.isPlaying && GUILayout.Button("현재 Scene의 CU/터렛 지역 바인딩 연결"))
                        RegionSceneSetup.InstallOnActiveScene();
                    return;
                }
                RegionGridSnapshot snapshot = binding.Source.Snapshot;
                EditorGUILayout.LabelField("점령 셀", snapshot.CapturedCellCount + " / " + snapshot.Definition.CapturableCellCount + " · " + snapshot.CaptureRatio.ToString("P1"));
                EditorGUILayout.LabelField("Grid revision", snapshot.Revision.ToString());
                using (new EditorGUI.DisabledGroupScope(!binding.HasWriteAuthority))
                {
                    var requested = (RegionPolicyMode)EditorGUILayout.EnumPopup("지역 정책", snapshot.Mode);
                    if (requested != snapshot.Mode && ConfirmReset())
                        binding.ResetPolicy(requested, true);
                    if (GUILayout.Button("CU 지역 Grid 설정 다시 읽기 / Reset") && ConfirmReset())
                        binding.ResetPolicy(snapshot.Mode, true);
                    if (snapshot.Mode == RegionPolicyMode.Static)
                    {
                        int highest = snapshot.UnlockedRegionIds.Count == 0 ? 0 : snapshot.UnlockedRegionIds.Max();
                        EditorGUILayout.LabelField("해금 구역", string.Join(", ", snapshot.UnlockedRegionIds));
                        if (GUILayout.Button("다음 정적 구역 해금 (점유 완료 아님)")) binding.TryUnlockThroughZone(highest + 1);
                    }
                }
                EditorGUILayout.HelpBox(snapshot.Mode == RegionPolicyMode.Dynamic
                    ? "CU 시작 셀에서 상하좌우로 연결된 투영 셀을 점령합니다. OFF/파괴 후에는 CU 시작 셀과 남은 연결만 유지하며 전력/냉각은 그대로 둡니다."
                    : "CU 거리 구역은 해금만 합니다. 전투 성과·생존 참가자·적 압력으로 확보하며 확보 완료 시 다음 구역이 열립니다. 물/이동 불가 셀은 진행률에서 제외합니다.",
                    MessageType.None);
                DrawValidationProtection(binding);
                if (snapshot.Mode == RegionPolicyMode.Static) DrawStaticCapture(binding);
                DrawCapturedCells = EditorGUILayout.Toggle("Scene 점령 Grid", DrawCapturedCells);
                DrawInputTriangles = EditorGUILayout.Toggle("임시 판정 삼각형", DrawInputTriangles);
                if (snapshot.Mode == RegionPolicyMode.Dynamic)
                {
                    if (!string.IsNullOrEmpty(binding.LastDynamicError))
                        EditorGUILayout.HelpBox(binding.LastDynamicError, MessageType.Error);
                    DrawStartCells(binding, snapshot);
                }
                if (snapshot.Mode == RegionPolicyMode.Dynamic && binding.LastEvaluation != null)
                {
                    EditorGUILayout.LabelField("활성 정점 / 임시 삼각형", binding.LastEvaluation.Geometry.Vertices.Count +
                        " / " + binding.LastEvaluation.Geometry.Triangles.Count);
                    EditorGUILayout.LabelField("CU 시작 / 연결 제외 셀", binding.LastEvaluation.InitialCellCount +
                        " / " + binding.LastEvaluation.DisconnectedCellCount);
                }
            }
        }

        private static void DrawValidationProtection(RegionSceneBinding binding)
        {
            var targets = new List<GameObject>();
            var root = binding.GetComponent<InitialStageCompositionRoot>();
            if (root?.LocalPlayerActors != null) targets.AddRange(root.LocalPlayerActors.Values.Where(actor => actor != null));
            if (binding.ControlUnit != null) targets.Add(binding.ControlUnit.gameObject);
            bool protectedNow = targets.Count > 0 && targets.All(actor => DebugDamageProtection.IsProtected(actor.transform));
            using (new EditorGUI.DisabledGroupScope(!EditorApplication.isPlaying || !binding.HasWriteAuthority))
            {
                bool requested = EditorGUILayout.Toggle("검증용 Player / CU 피해 보호", protectedNow);
                if (requested != protectedNow)
                    foreach (var actor in targets)
                    {
                        var marker = actor.GetComponent<DebugDamageProtection>();
                        if (requested) (marker != null ? marker : actor.AddComponent<DebugDamageProtection>()).enabled = true;
                        else if (marker != null) { marker.enabled = false; UnityEngine.Object.Destroy(marker); }
                    }
            }
            if (protectedNow) EditorGUILayout.HelpBox("개발 검증용 피해 보호 중입니다. 피해/승패 검증과 구분하세요. Play 종료 시 해제됩니다.", MessageType.Warning);
        }

        private static void DrawStaticCapture(RegionSceneBinding binding)
        {
            EditorGUILayout.LabelField("정적 확보 · 실제 전투 관측", EditorStyles.boldLabel);
            var settings = binding.CaptureSettings;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_captureScroll, GUILayout.MaxHeight(220)))
            {
                _captureScroll = scroll.scrollPosition;
                foreach (var state in binding.StaticCaptureProgress)
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"구역 {state.RegionId} · {CaptureReason(state.Reason)}", EditorStyles.boldLabel);
                        Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                        EditorGUI.ProgressBar(rect, (float)(state.HoldSeconds / settings.HoldSeconds),
                            $"확보 {state.HoldSeconds:F1} / {settings.HoldSeconds:F0} 초");
                        EditorGUILayout.LabelField($"팀 피해 {state.TeamDamage:F0}/{settings.MinimumTeamDamage:F0} · 생존 아군 {state.LivingParticipants} / 적 {state.LivingEnemies}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"이탈 {state.AbsenceSeconds:F1}s · 적 우세 {state.SuperioritySeconds:F1}s", EditorStyles.miniLabel);
                    }
            }
            _showCaptureSettings = EditorGUILayout.Foldout(_showCaptureSettings, "확보 시험값 / 현재 Match 적용", true);
            if (!_showCaptureSettings) return;
            using (new EditorGUI.DisabledGroupScope(!binding.HasWriteAuthority))
            {
                EditorGUI.BeginChangeCheck();
                float hold = EditorGUILayout.FloatField("확보 유지 (게임 초)", (float)settings.HoldSeconds);
                float abandon = EditorGUILayout.FloatField("이탈 유예 (게임 초)", (float)settings.AbandonSeconds);
                float loss = EditorGUILayout.FloatField("연속 적 우세 / 상실 (초)", (float)settings.LossSeconds);
                float damage = EditorGUILayout.FloatField("최소 팀 피해", (float)settings.MinimumTeamDamage);
                int pressure = EditorGUILayout.IntField("참가자당 허용 적 수", settings.EnemiesPerParticipant);
                if (EditorGUI.EndChangeCheck())
                    try { binding.ConfigureCapture(new StaticRegionCaptureSettings(hold, abandon, loss, damage, pressure)); }
                    catch (ArgumentException error) { Debug.LogWarning("확보 시험값을 적용하지 않았습니다: " + error.Message); }
            }
        }

        private static string CaptureReason(StaticRegionCaptureReason reason) => reason switch
        {
            StaticRegionCaptureReason.Locked => "미해금",
            StaticRegionCaptureReason.NoParticipant => "참가자 없음 · 일시정지",
            StaticRegionCaptureReason.NeedsCombat => "전투 성과 필요",
            StaticRegionCaptureReason.EnemyPressure => "적 우세 · 일시정지",
            StaticRegionCaptureReason.Holding => "확보 유지 중",
            StaticRegionCaptureReason.Secured => "확보 완료",
            StaticRegionCaptureReason.Lost => "확보 상실 · 해금 유지",
            StaticRegionCaptureReason.Abandoned => "이탈 유예 종료 · 시도 초기화",
            _ => reason.ToString()
        };

        private static void DrawStartCells(RegionSceneBinding binding, RegionGridSnapshot snapshot)
        {
            _showStartCells = EditorGUILayout.Foldout(_showStartCells, "동적 시작 영역 · Host 설정", true);
            if (!_showStartCells) return;
            if (!binding.HasWriteAuthority)
            {
                EditorGUILayout.HelpBox("시작 영역은 Host가 설정합니다. Client는 받은 점유 셀을 표시합니다.", MessageType.Info);
                return;
            }
            if (_draftBinding != binding || _draftGrid != snapshot.GridId)
            {
                _draftBinding = binding;
                _draftGrid = snapshot.GridId;
                _startDraft.Clear();
                foreach (int cell in binding.CaptureDynamicStartCells())
                    _startDraft.Add(new Vector2Int(cell % snapshot.Definition.Geometry.CellsX,
                        cell / snapshot.Definition.Geometry.CellsX));
                _startPage = 0;
                _startError = null;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("빈 목록은 CU 셀 하나입니다. 명시 목록은 CU 셀을 포함하고 상하좌우로 이어져야 합니다. 적용 전에는 실제 점유가 바뀌지 않습니다.", MessageType.None);
                const int pageSize = 32;
                int pages = Math.Max(1, (_startDraft.Count + pageSize - 1) / pageSize);
                _startPage = Mathf.Clamp(_startPage, 0, pages - 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"셀 {_startDraft.Count}개 · {_startPage + 1}/{pages}");
                    using (new EditorGUI.DisabledGroupScope(_startPage == 0))
                        if (GUILayout.Button("이전", GUILayout.Width(45))) _startPage--;
                    using (new EditorGUI.DisabledGroupScope(_startPage + 1 >= pages))
                        if (GUILayout.Button("다음", GUILayout.Width(45))) _startPage++;
                }
                _startScroll = EditorGUILayout.BeginScrollView(_startScroll, GUILayout.MaxHeight(150));
                for (int i = _startPage * pageSize; i < Math.Min(_startDraft.Count, (_startPage + 1) * pageSize); i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _startDraft[i] = EditorGUILayout.Vector2IntField("Grid X/Y · " + i, _startDraft[i]);
                        if (GUILayout.Button("−", GUILayout.Width(25))) { _startDraft.RemoveAt(i); break; }
                    }
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledGroupScope(_startDraft.Count >= snapshot.Definition.Geometry.TotalCellCount))
                        if (GUILayout.Button("셀 추가"))
                        {
                            Vector3 cu = binding.ControlUnit.position;
                            snapshot.Definition.Geometry.TryMapPoint(new BattlefieldPoint(cu.x, cu.y), out int x, out int y);
                            _startDraft.Add(new Vector2Int(x, y));
                            _startPage = (_startDraft.Count - 1) / pageSize;
                        }
                    if (GUILayout.Button("CU 1셀 기본값")) _startDraft.Clear();
                }
                if (GUILayout.Button("시작 셀 적용 / 점유 Reset") && ConfirmReset())
                {
                    try
                    {
                        int[] cells = _startDraft.Select(cell => snapshot.Definition.GetCellIndex(cell.x, cell.y)).ToArray();
                        binding.TrySetDynamicStartCells(cells, true, out _startError);
                    }
                    catch (ArgumentException error) { _startError = error.Message; }
                }
                if (!string.IsNullOrEmpty(_startError)) EditorGUILayout.HelpBox(_startError, MessageType.Warning);
            }
        }

        private static bool ConfirmReset() => EditorUtility.DisplayDialog("지역 정책 / Grid 초기화",
            "점령 데이터를 초기화하고 새 Grid 세대로 계산합니다. Static으로 전환하면 구역 밖 터렛이 잠기고 꺼질 수 있습니다. 계속할까요?",
            "초기화 후 적용", "취소");
    }
}
