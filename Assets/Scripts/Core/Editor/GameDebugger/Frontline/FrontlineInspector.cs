// Overlay와 Game Debugger가 재사용할 전선 선택 Inspector를 그립니다.
// 모든 항목은 현재 Core snapshot의 기하 진단이며 전투 우세를 추정하지 않습니다.
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class FrontlineInspector
    {
        private static bool _isExpanded = true;
        public static void Draw()
        {
            FrontlineInspectionSession.Refresh();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _isExpanded = EditorGUILayout.Foldout(_isExpanded, "Frontline geometry", true);
                if (!_isExpanded) return;
                FrontlineGeometryReport report = FrontlineInspectionSession.Report;
                if (report == null)
                {
                    EditorGUILayout.HelpBox("현재 공간 snapshot이 없습니다.", MessageType.Info);
                    return;
                }
                EditorGUILayout.LabelField($"{BattlefieldDebugSession.Mode} · r{report.Snapshot.Revision}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Area / perimeter", $"{report.Area:F2} u² / {report.Perimeter:F2} u");
                EditorGUILayout.LabelField("Triangles / groups / edges", $"{report.Snapshot.Triangles.Count} / {report.ComponentCount} / {report.Edges.Count}");
                bool enabled = EditorGUILayout.ToggleLeft("Pick boundary edge in Scene View", FrontlineInspectionSession.IsPickingEnabled);
                FrontlineInspectionSession.SetPickingEnabled(enabled);
                FrontlineEdgeInspection edge = FrontlineInspectionSession.Selected;
                if (edge == null)
                {
                    EditorGUILayout.HelpBox(report.Edges.Count == 0
                        ? "경계가 없습니다. 비공선 터렛 3개 이상과 등록 상태를 확인하세요."
                        : "선택 모드를 켜고 주황 경계선 근처를 클릭하세요. Alt 탐색은 유지됩니다.", MessageType.Info);
                    return;
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Selected edge", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(edge.Edge.First.Value + " ↔ " + edge.Edge.Second.Value,
                        EditorStyles.wordWrappedLabel, GUILayout.Height(36));
                    EditorGUILayout.LabelField("Length / triangle / group", $"{edge.Length:F2} u / {edge.TriangleIndex} / {edge.ComponentIndex}");
                    EditorGUILayout.LabelField("Midpoint XY", Format(edge.Midpoint));
                    EditorGUILayout.LabelField("Outward / inward", Format(edge.OutwardNormal) + " / " + Format(edge.InwardNormal));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Frame")) Frame(edge);
                        if (GUILayout.Button("Copy")) EditorGUIUtility.systemCopyBuffer =
                            $"{BattlefieldDebugSession.Mode} Match {report.Snapshot.MatchId} r{report.Snapshot.Revision}\n" +
                            $"Edge {edge.Edge.First.Value} ↔ {edge.Edge.Second.Value}\nLength {edge.Length:F3}\n" +
                            $"Midpoint {Format(edge.Midpoint)}\nOutward {Format(edge.OutwardNormal)}\n" +
                            $"Triangle {edge.TriangleIndex} / component {edge.ComponentIndex}\nGeometry only; tactical pressure unavailable.";
                        if (GUILayout.Button("Clear")) FrontlineInspectionSession.Select(null);
                    }
                }
                EditorGUILayout.HelpBox("법선은 안↔밖 기하 방향입니다. 사거리·위험도·전투 우세를 나타내지 않습니다.", MessageType.None);
            }
        }

        private static string Format(Vector3 value) => $"({value.x:F2}, {value.y:F2})";
        private static void Frame(FrontlineEdgeInspection edge) => SceneView.lastActiveSceneView?.Frame(
            new Bounds(edge.Midpoint, Vector3.one * Mathf.Max(5f, (float)edge.Length * 1.3f)), false);
    }
}
