// Window와 Overlay가 공유하는 레이어 조작·읽기 전용 상세·캡처 UI입니다.
// 게임 정책을 계산하거나 Feature의 진단 결과를 추측하지 않습니다.
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Editor
{
    public static class BattlefieldDiagnosticPanels
    {
        public static void DrawDisplayControls()
        {
            EditorGUI.BeginChangeCheck();
            BattlefieldDebugDisplaySettings display = BattlefieldDebugSession.Display;
            display.IsEnabled = EditorGUILayout.Toggle("Scene layers", display.IsEnabled);
            display.ShowGrid = EditorGUILayout.Toggle("Uniform Grid", display.ShowGrid);
            display.ShowTerritory = EditorGUILayout.Toggle("Territory", display.ShowTerritory);
            display.ShowFrontline = EditorGUILayout.Toggle("Frontline", display.ShowFrontline);
            display.ShowOccupancy = EditorGUILayout.Toggle("Cell occupancy", display.ShowOccupancy);
            display.ShowBoundaryNormals = EditorGUILayout.Toggle("Geometric normals", display.ShowBoundaryNormals);
            display.ShowVertexLabels = EditorGUILayout.Toggle("Vertex labels", display.ShowVertexLabels);
            display.IsCellPickingEnabled = EditorGUILayout.Toggle("Pick cell (left click)", display.IsCellPickingEnabled);
            display.IsCaptureMode = EditorGUILayout.Toggle("Capture mode", display.IsCaptureMode);
            if (EditorGUI.EndChangeCheck()) BattlefieldDebugSession.NotifyDisplayChanged();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Topology")) BattlefieldDebugSession.ApplyPreset("Topology");
            if (GUILayout.Button("Grid")) BattlefieldDebugSession.ApplyPreset("Grid");
            if (GUILayout.Button("Capture")) BattlefieldDebugSession.ApplyPreset("Capture");
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawOverview()
        {
            BattlefieldSpatialSnapshot snapshot = BattlefieldDebugSession.Snapshot;
            EditorGUILayout.LabelField(BattlefieldDebugSession.Mode + " / " + SceneManager.GetActiveScene().name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Match", snapshot == null || snapshot.MatchId.IsEmpty ? "N/A" : snapshot.MatchId.ToString());
            EditorGUILayout.LabelField("Revision / V / T / F",
                $"{snapshot?.Revision ?? 0} / {snapshot?.Vertices.Count ?? 0} / {snapshot?.Triangles.Count ?? 0} / {snapshot?.FrontlineEdges.Count ?? 0}");
            if (!string.IsNullOrEmpty(BattlefieldDebugSession.Error))
                EditorGUILayout.HelpBox(BattlefieldDebugSession.Error, MessageType.Error);
            if (BattlefieldDebugSession.Mode == "UNAVAILABLE")
                EditorGUILayout.HelpBox("현재 Match가 없습니다. 이전 Match의 값은 제거했습니다.", MessageType.Warning);
            if (snapshot != null && snapshot.Triangles.Count == 0)
                EditorGUILayout.HelpBox("Territory/Frontline 없음: 유효한 비공선 터렛 3개 이상이 필요합니다. 전선 우세도: N/A.", MessageType.Warning);
        }

        public static void DrawCellDetails()
        {
            BattlefieldGridSnapshot grid = BattlefieldDebugSession.Grid;
            if (grid == null)
            {
                EditorGUILayout.HelpBox("조회 가능한 Grid가 없습니다.", MessageType.None);
                return;
            }
            EditorGUILayout.LabelField("Selected Cell (read-only)", EditorStyles.boldLabel);
            int selected = EditorGUILayout.IntField("Cell ID (-1 = none)", BattlefieldDebugSession.SelectedCellId);
            if (selected != BattlefieldDebugSession.SelectedCellId) BattlefieldDebugSession.SelectCell(selected);
            selected = BattlefieldDebugSession.SelectedCellId;
            if (selected >= 0 && selected < grid.Cells.Count)
            {
                BattlefieldGridCellSnapshot cell = grid.Cells[selected];
                BattlefieldGridConfiguration configuration = grid.Configuration;
                double minX = configuration.MinimumX + cell.X * configuration.CellWidth;
                double minY = configuration.MinimumY + cell.Y * configuration.CellHeight;
                EditorGUILayout.LabelField("Index X/Y", $"{cell.X} / {cell.Y}");
                EditorGUILayout.LabelField("Bounds X", $"{minX:F2} .. {minX + configuration.CellWidth:F2}");
                EditorGUILayout.LabelField("Bounds Y", $"{minY:F2} .. {minY + configuration.CellHeight:F2}");
                DrawIds("Turret", cell.TurretIds);
                DrawIds("Player", cell.PlayerIds);
                DrawIds("Enemy", cell.EnemyIds);
                if (!EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("PREVIEW: 정적 터렛 배치만 집계합니다. Player/Enemy는 LIVE 입력에서 조회됩니다.", MessageType.None);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Out of Bounds IDs", EditorStyles.boldLabel);
            DrawIds("Turret OOB", grid.OutOfBoundsTurretIds);
            DrawIds("Player OOB", grid.OutOfBoundsPlayerIds);
            DrawIds("Enemy OOB", grid.OutOfBoundsEnemyIds);
        }

        private static void DrawIds(string label, IReadOnlyList<EntityId> ids)
        {
            var text = new System.Text.StringBuilder();
            int count = Mathf.Min(ids.Count, 32);
            for (int index = 0; index < count; index++)
            {
                if (index > 0) text.Append(", ");
                text.Append(ids[index]);
            }
            if (ids.Count > count) text.Append($" … (+{ids.Count - count})");
            EditorGUILayout.LabelField($"{label} ({ids.Count})", ids.Count == 0 ? "—" : text.ToString(), EditorStyles.wordWrappedLabel);
        }

        public static void DrawCapture()
        {
            BattlefieldDebugSession.Display.CaptureLabel = EditorGUILayout.TextField(
                "Commit / Build label", BattlefieldDebugSession.Display.CaptureLabel);
            if (GUILayout.Button("Copy capture metadata"))
                EditorGUIUtility.systemCopyBuffer = BattlefieldDebugSession.CreateCaptureMetadata();
            EditorGUILayout.HelpBox("스크린샷/영상은 Scene View와 Game View를 함께 기록하세요. 캡처 라벨은 직접 입력하며 Build/권한/Plan의 미연결 값은 N/A로 남깁니다.", MessageType.None);
        }
    }
}
