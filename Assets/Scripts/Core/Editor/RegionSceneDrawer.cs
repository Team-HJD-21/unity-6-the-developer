// Scene View에 Match가 확정한 지역 Grid와 선택적인 임시 삼각형을 그립니다.
// 표시용 계산을 게임 상태에 쓰지 않으며 큰 Grid는 표시 수만 제한합니다.
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class RegionSceneDrawer
    {
        private const int MaximumDrawnCells = 4096;
        static RegionSceneDrawer() => SceneView.duringSceneGui += Draw;

        private static void Draw(SceneView view)
        {
            if (!EditorApplication.isPlaying) return;
            var binding = RegionDebugControls.FindBinding();
            if (binding == null) return;
            RegionGridSnapshot snapshot = binding.Source.Snapshot;
            BattlefieldGridConfiguration grid = snapshot.Definition.Geometry;
            Color previous = Handles.color;
            try
            {
                if (RegionDebugControls.DrawCapturedCells)
                {
                    var corners = new Vector3[4];
                    int drawn = 0;
                    for (int index = 0; index < snapshot.CapturedCells.Count && drawn < MaximumDrawnCells; index++)
                    {
                        if (!snapshot.CapturedCells[index]) continue;
                        int x = index % grid.CellsX, y = index / grid.CellsX;
                        float left = (float)(grid.MinimumX + x * grid.CellWidth);
                        float bottom = (float)(grid.MinimumY + y * grid.CellHeight);
                        float right = (float)(grid.MinimumX + (x + 1) * grid.CellWidth);
                        float top = (float)(grid.MinimumY + (y + 1) * grid.CellHeight);
                        float z = (float)grid.OriginZ;
                        corners[0] = new Vector3(left, bottom, z); corners[1] = new Vector3(right, bottom, z);
                        corners[2] = new Vector3(right, top, z); corners[3] = new Vector3(left, top, z);
                        Handles.DrawSolidRectangleWithOutline(corners, new Color(.2f, .9f, .45f, .12f), new Color(.2f, .9f, .45f, .7f));
                        drawn++;
                    }
                    if (snapshot.CapturedCellCount > MaximumDrawnCells)
                        Handles.Label(new Vector3((float)grid.Origin.X, (float)grid.Origin.Y, (float)grid.OriginZ),
                            "지역 Grid: " + drawn + "/" + snapshot.CapturedCellCount + " 셀 표시 (데이터 제한 아님)");
                }
                if (RegionDebugControls.DrawInputTriangles && binding.LastEvaluation != null)
                {
                    var vertices = new Dictionary<EntityId, Vector3>();
                    foreach (BattlefieldVertex vertex in binding.LastEvaluation.Geometry.Vertices)
                        vertices.Add(vertex.EntityId, new Vector3((float)vertex.Position.X, (float)vertex.Position.Y, (float)grid.OriginZ));
                    Handles.color = new Color(1, .7f, .2f, .8f);
                    foreach (BattlefieldTriangle triangle in binding.LastEvaluation.Geometry.Triangles)
                        Handles.DrawAAPolyLine(vertices[triangle.VertexA], vertices[triangle.VertexB],
                            vertices[triangle.VertexC], vertices[triangle.VertexA]);
                }
            }
            finally { Handles.color = previous; }
        }
    }
}
