// Game Debugger에서 지역 정책을 선택하고 확정 Grid와 임시 판정 자료를 구분해 표시합니다.
// 실제 변경은 Match 바인딩에 요청하며 Editor가 별도 지역 상태를 만들지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class RegionDebugControls
    {
        public static bool DrawCapturedCells = true;
        public static bool DrawInputTriangles;
        private static bool _showStartCells;
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
                EditorGUILayout.LabelField("점령 셀", snapshot.CapturedCellCount + " / " + snapshot.CapturedCells.Count);
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
                        int highest = 0;
                        for (int index = 0; index < snapshot.CapturedCells.Count; index++)
                            if (snapshot.CapturedCells[index]) highest = Mathf.Max(highest, snapshot.Definition.RegionIds[index]);
                        if (GUILayout.Button("다음 정적 구역 해금")) binding.TryUnlockThroughZone(highest + 1);
                    }
                }
                EditorGUILayout.HelpBox(snapshot.Mode == RegionPolicyMode.Dynamic
                    ? "CU 시작 셀에서 상하좌우로 연결된 투영 셀을 점령합니다. OFF/파괴 후에는 CU 시작 셀과 남은 연결만 유지하며 전력/냉각은 그대로 둡니다."
                    : "기존 CU 거리 구역을 지역 Grid로 적용합니다. 아래 Battlefield Grid는 참가자 밀도용이며 지역 점령과 다른 데이터입니다.",
                    MessageType.None);
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
