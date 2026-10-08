// Editor의 공유 진단 상태를 Scene View에 그립니다. 창 수명과 독립적으로 등록됩니다.
// 도형은 Core 결과를 읽을 뿐 게임 정책이나 Match 상태를 변경하지 않습니다.
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DomainEntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class BattlefieldSceneDrawer
    {
        static BattlefieldSceneDrawer()
        {
            SceneView.duringSceneGui += DrawSceneView;
            AssemblyReloadEvents.beforeAssemblyReload += () => SceneView.duringSceneGui -= DrawSceneView;
        }

        private static void DrawSceneView(SceneView sceneView)
        {
            if (!sceneView.drawGizmos || !BattlefieldDebugSession.Display.IsEnabled) return;
            if (BattlefieldDebugSession.Display.IsCellPickingEnabled)
                FrontlineInspectionSession.YieldToCellPicking();
            PickFrontline();
            PickCell();
            if (Event.current.type != EventType.Repaint) return;
            BattlefieldGridConfiguration gridConfiguration = ResolveGridConfiguration();
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                if (BattlefieldDebugSession.Display.ShowGrid && gridConfiguration != null) DrawGrid(gridConfiguration);
                DrawSelectedCell();
                if (BattlefieldDebugSession.Snapshot == null) return;
                if (EditorApplication.isPlaying && BattlefieldDebugSession.AppRoot == null) return;

                var positions = new Dictionary<DomainEntityId, Vector3>(BattlefieldDebugSession.Snapshot.Vertices.Count);
                foreach (var vertex in BattlefieldDebugSession.Snapshot.Vertices)
                    positions.Add(vertex.EntityId, ToWorldPosition(vertex.Position));

                if (BattlefieldDebugSession.Display.ShowTerritory) DrawTerritory(positions);
                if (BattlefieldDebugSession.Display.ShowFrontline) DrawFrontlines(positions);
                if (BattlefieldDebugSession.Display.ShowFrontline) DrawSelectedFrontline();
                if (BattlefieldDebugSession.Display.ShowVertexLabels) DrawVertexLabels(positions);
                if (BattlefieldDebugSession.Display.ShowBoundaryNormals) DrawBoundaryNormals(positions);
                if (BattlefieldDebugSession.Display.ShowOccupancy) DrawOccupancy();
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
                DrawLegend(sceneView);
            }
        }

        private static void PickFrontline()
        {
            if (!FrontlineInspectionSession.IsPickingEnabled || !BattlefieldDebugSession.Display.ShowFrontline ||
                BattlefieldDebugSession.Display.IsCaptureMode || Event.current.alt) return;
            FrontlineInspectionSession.Refresh();
            FrontlineGeometryReport report = FrontlineInspectionSession.Report;
            if (report == null) return;
            foreach (FrontlineEdgeInspection edge in report.Edges)
            {
                int control = GUIUtility.GetControlID(FocusType.Passive);
                float distance = HandleUtility.DistanceToLine(edge.FirstPosition, edge.SecondPosition);
                if (Event.current.type == EventType.Layout && distance <= 12f)
                    HandleUtility.AddControl(control, distance);
                if (Event.current.type != EventType.MouseDown || Event.current.button != 0 ||
                    HandleUtility.nearestControl != control || distance > 12f) continue;
                FrontlineInspectionSession.Select(edge.Edge);
                Event.current.Use();
                break;
            }
        }

        private static void DrawSelectedFrontline()
        {
            if (BattlefieldDebugSession.Display.IsCaptureMode) return;
            FrontlineEdgeInspection edge = FrontlineInspectionSession.Selected;
            if (edge == null) return;
            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(6f, edge.FirstPosition, edge.SecondPosition);
            float length = Mathf.Min((float)edge.Length * 0.2f, HandleUtility.GetHandleSize(edge.Midpoint) * 1.4f);
            if (length > 0.0001f)
            {
                Handles.color = new Color(1f, 0.6f, 0.2f);
                Handles.ArrowHandleCap(0, edge.Midpoint, Quaternion.LookRotation(edge.OutwardNormal, Vector3.forward), length, EventType.Repaint);
                Handles.color = Color.cyan;
                Handles.ArrowHandleCap(0, edge.Midpoint, Quaternion.LookRotation(edge.InwardNormal, Vector3.forward), length, EventType.Repaint);
            }
            Handles.color = Color.white;
            Handles.Label(edge.Midpoint, $"{edge.Edge.First} ↔ {edge.Edge.Second}\n{edge.Length:F2} u · triangle {edge.TriangleIndex} · group {edge.ComponentIndex}");
        }

        private static void PickCell()
        {
            if (!BattlefieldDebugSession.Display.IsCellPickingEnabled ||
                BattlefieldDebugSession.Display.IsCaptureMode || Event.current.alt) return;
            BattlefieldGridConfiguration configuration = ResolveGridConfiguration();
            if (configuration == null) return;
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0, 0, (float)configuration.OriginZ));
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            int cellId = configuration.TryMapPoint(new BattlefieldPoint(point.x, point.y), out int x, out int y)
                ? y * configuration.CellsX + x : -1;
            BattlefieldDebugSession.SelectCell(cellId);
            Event.current.Use();
        }

        private static void DrawSelectedCell()
        {
            BattlefieldGridSnapshot grid = BattlefieldDebugSession.Grid;
            int selected = BattlefieldDebugSession.SelectedCellId;
            if (grid == null || selected < 0 || selected >= grid.Cells.Count ||
                BattlefieldDebugSession.Display.IsCaptureMode) return;
            BattlefieldGridCellSnapshot cell = grid.Cells[selected];
            Handles.color = Color.yellow;
            Vector3[] corners = CellCorners(grid.Configuration, cell);
            Handles.DrawAAPolyLine(3f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            Handles.Label((corners[0] + corners[2]) * 0.5f,
                $"Cell {cell.CellId} ({cell.X}, {cell.Y})\nT:{cell.TurretCount} P:{cell.PlayerCount} E:{cell.EnemyCount}");
        }

        private static Vector3[] CellCorners(BattlefieldGridConfiguration configuration, BattlefieldGridCellSnapshot cell)
        {
            float x = (float)(configuration.MinimumX + cell.X * configuration.CellWidth);
            float y = (float)(configuration.MinimumY + cell.Y * configuration.CellHeight);
            float width = (float)configuration.CellWidth;
            float height = (float)configuration.CellHeight;
            float z = (float)configuration.OriginZ;
            return new[] { new Vector3(x, y, z), new Vector3(x + width, y, z),
                new Vector3(x + width, y + height, z), new Vector3(x, y + height, z) };
        }

        private static void DrawBoundaryNormals(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            BattlefieldSpatialSnapshot snapshot = BattlefieldDebugSession.Snapshot;
            Handles.color = new Color(1f, 0.6f, 0.2f);
            foreach (BattlefieldEdgeAdjacency adjacency in snapshot.EdgeAdjacencies)
            {
                if (!adjacency.IsBoundary) continue;
                Vector3 first = positions[adjacency.Edge.First];
                Vector3 second = positions[adjacency.Edge.Second];
                Vector3 midpoint = (first + second) * 0.5f;
                BattlefieldTriangle triangle = snapshot.Triangles[adjacency.FirstTriangleIndex];
                Vector3 centroid = (positions[triangle.VertexA] + positions[triangle.VertexB] + positions[triangle.VertexC]) / 3f;
                Vector3 normal = new Vector3(-(second.y - first.y), second.x - first.x, 0f).normalized;
                if (Vector3.Dot(normal, midpoint - centroid) < 0) normal = -normal;
                // 법선은 외곽 기하의 방향일 뿐 공격/방어 우세 신호가 아닙니다.
                float length = Mathf.Min(Vector3.Distance(first, second) * 0.2f, HandleUtility.GetHandleSize(midpoint));
                if (length <= 0.0001f) continue;
                Handles.ArrowHandleCap(0, midpoint, Quaternion.LookRotation(normal, Vector3.forward), length, EventType.Repaint);
            }
        }

        private static void DrawLegend(SceneView sceneView)
        {
            Handles.BeginGUI();
            try
            {
                float width = Mathf.Max(160f, Mathf.Min(430f, sceneView.position.width - 24f));
                var area = new Rect(12, Mathf.Max(12, sceneView.position.height - 148), width, 112);
                GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
                var snapshot = BattlefieldDebugSession.Snapshot;
                string match = snapshot == null || snapshot.MatchId.IsEmpty ? "N/A" : snapshot.MatchId.ToString();
                if (match.Length > 10) match = match.Substring(0, 10);
                // Scene 그리기는 Repaint에만 실행되므로 GUILayout의 Layout 캐시를 사용하지 않습니다.
                GUI.Label(new Rect(area.x + 6, area.y + 4, width - 12, 20),
                    $"{BattlefieldDebugSession.Mode} | {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} | Match {match} r{snapshot?.Revision ?? 0}", EditorStyles.boldLabel);
                GUI.Label(new Rect(area.x + 6, area.y + 24, width - 12, 20), BattlefieldDebugSession.Display.CaptureLabel);
                var configuration = ResolveGridConfiguration();
                GUI.Label(new Rect(area.x + 6, area.y + 44, width - 12, 20), configuration == null ? "Grid N/A" :
                    $"Grid {configuration.MapWidth}×{configuration.MapHeight} / {configuration.CellsX}×{configuration.CellsY} cells");
                GUI.Label(new Rect(area.x + 6, area.y + 64, width - 12, 20), "청록 Territory · 주황 Frontline · 노랑 선택 셀 · E 원시 적 수");
                GUI.Label(new Rect(area.x + 6, area.y + 84, width - 12, 20), "법선 = 안→밖 기하 방향 / 우세·권한·PlanId = N/A");
            }
            finally { Handles.EndGUI(); }
        }

        private static void DrawTerritory(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = new Color(0.1f, 0.75f, 1f, 0.12f);
            foreach (var triangle in BattlefieldDebugSession.Snapshot.Triangles)
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

        private static void DrawFrontlines(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = GameDebugVisualStyle.Frontline;
            foreach (var edge in BattlefieldDebugSession.Snapshot.FrontlineEdges)
                Handles.DrawAAPolyLine(3f, positions[edge.First], positions[edge.Second]);
        }

        private static void DrawVertexLabels(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = Color.white;
            int count = 0;
            foreach (var vertex in BattlefieldDebugSession.Snapshot.Vertices)
            {
                if (++count > 128) break;
                Handles.Label(positions[vertex.EntityId], vertex.EntityId.ToString());
            }
        }

        private static Vector3 ToWorldPosition(BattlefieldPoint point)
        {
            BattlefieldGridConfiguration configuration = ResolveGridConfiguration();
            return new Vector3((float)point.X, (float)point.Y,
                (float)(configuration == null ? 0d : configuration.OriginZ));
        }

        private static BattlefieldGridConfiguration ResolveGridConfiguration() => BattlefieldDebugSession.Configuration;

        private static void DrawGrid(BattlefieldGridConfiguration configuration)
        {
            var minX = (float)configuration.MinimumX;
            var minY = (float)configuration.MinimumY;
            var maxX = (float)configuration.MaximumX;
            var maxY = (float)configuration.MaximumY;
            var z = (float)configuration.OriginZ;
            Handles.color = new Color(0.75f, 0.85f, 1f, 0.55f);
            int stepX = Mathf.Max(1, Mathf.CeilToInt(configuration.CellsX / 1024f));
            int stepY = Mathf.Max(1, Mathf.CeilToInt(configuration.CellsY / 1024f));
            for (var x = 0; x <= configuration.CellsX; x += stepX)
            {
                var worldX = minX + x * (float)configuration.CellWidth;
                Handles.DrawAAPolyLine(1f, new Vector3(worldX, minY, z), new Vector3(worldX, maxY, z));
            }
            for (var y = 0; y <= configuration.CellsY; y += stepY)
            {
                var worldY = minY + y * (float)configuration.CellHeight;
                Handles.DrawAAPolyLine(1f, new Vector3(minX, worldY, z), new Vector3(maxX, worldY, z));
            }
            Handles.DrawAAPolyLine(1.5f, new Vector3(minX, minY, z), new Vector3(maxX, minY, z),
                new Vector3(maxX, maxY, z), new Vector3(minX, maxY, z), new Vector3(minX, minY, z));
        }

        private static void DrawOccupancy()
        {
            BattlefieldGridSnapshot grid = BattlefieldDebugSession.Grid;
            if (grid == null) return;
            int count = 0;
            foreach (var cell in grid.OccupiedCells)
            {
                if (++count > 2048) break;
                var configuration = grid.Configuration;
                var x = configuration.MinimumX + (cell.X + 0.5d) * configuration.CellWidth;
                var y = configuration.MinimumY + (cell.Y + 0.5d) * configuration.CellHeight;
                var position = new Vector3((float)x, (float)y, (float)configuration.OriginZ);
                Vector3[] corners = CellCorners(configuration, cell);
                // 색은 해당 셀의 Enemy 원시 수만 표시합니다. 위험도/우세도가 아닙니다.
                Handles.color = cell.EnemyCount > 0 ? new Color(1f, 0.4f, 0.1f, 0.18f) : new Color(0.2f, 0.8f, 1f, 0.14f);
                Handles.DrawAAConvexPolygon(corners);
                Vector2 screenMin = HandleUtility.WorldToGUIPoint(corners[0]);
                Vector2 screenMax = HandleUtility.WorldToGUIPoint(corners[2]);
                if (!BattlefieldDebugSession.Display.IsCaptureMode && count <= 128 &&
                    Mathf.Abs(screenMax.x - screenMin.x) >= 48f && Mathf.Abs(screenMax.y - screenMin.y) >= 24f)
                    Handles.Label(position, $"T:{cell.TurretCount} P:{cell.PlayerCount} E:{cell.EnemyCount}");
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
