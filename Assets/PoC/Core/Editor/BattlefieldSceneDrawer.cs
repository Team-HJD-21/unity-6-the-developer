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
            if (!BattlefieldDebugSession.Display.IsEnabled || Event.current.type != EventType.Repaint) return;
            BattlefieldGridConfiguration gridConfiguration = ResolveGridConfiguration();
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                if (BattlefieldDebugSession.Display.ShowGrid && gridConfiguration != null) DrawGrid(gridConfiguration);
                if (BattlefieldDebugSession.Snapshot == null) return;
                if (EditorApplication.isPlaying && BattlefieldDebugSession.AppRoot == null) return;

                var positions = new Dictionary<DomainEntityId, Vector3>(BattlefieldDebugSession.Snapshot.Vertices.Count);
                foreach (var vertex in BattlefieldDebugSession.Snapshot.Vertices)
                    positions.Add(vertex.EntityId, ToWorldPosition(vertex.Position));

                if (BattlefieldDebugSession.Display.ShowTerritory) DrawTerritory(positions);
                if (BattlefieldDebugSession.Display.ShowFrontline) DrawFrontlines(positions);
                if (BattlefieldDebugSession.Display.ShowVertexLabels) DrawVertexLabels(positions);
                if (EditorApplication.isPlaying && BattlefieldDebugSession.Display.ShowOccupancy) DrawOccupancy();
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
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
            Handles.color = new Color(1f, 0.35f, 0.1f, 1f);
            foreach (var edge in BattlefieldDebugSession.Snapshot.FrontlineEdges)
                Handles.DrawAAPolyLine(3f, positions[edge.First], positions[edge.Second]);
        }

        private static void DrawVertexLabels(IReadOnlyDictionary<DomainEntityId, Vector3> positions)
        {
            Handles.color = Color.white;
            foreach (var vertex in BattlefieldDebugSession.Snapshot.Vertices)
                Handles.Label(positions[vertex.EntityId], vertex.EntityId.ToString());
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

        private static void DrawOccupancy()
        {
            foreach (var cell in BattlefieldDebugSession.Snapshot.Grid.OccupiedCells)
            {
                var configuration = BattlefieldDebugSession.Snapshot.Grid.Configuration;
                var x = configuration.MinimumX + (cell.X + 0.5d) * configuration.CellWidth;
                var y = configuration.MinimumY + (cell.Y + 0.5d) * configuration.CellHeight;
                var position = new Vector3((float)x, (float)y, (float)configuration.OriginZ);
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

