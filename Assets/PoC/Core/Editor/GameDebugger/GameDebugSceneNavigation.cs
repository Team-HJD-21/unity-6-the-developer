// 현재 Grid·선택 셀의 Scene 이동과 공유용 좌표 정보를 제공합니다.
// 조회 결과를 화면 이동에만 사용하며 Match·Transform·authoring 데이터를 변경하지 않습니다.
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
using System.Text;

namespace TeamHJD.Game.Editor
{
    public static class GameDebugSceneNavigation
    {
        public static Bounds GridBounds(BattlefieldGridConfiguration grid) => new Bounds(
            new Vector3((float)grid.Origin.X, (float)grid.Origin.Y, (float)grid.OriginZ),
            new Vector3((float)grid.MapWidth, (float)grid.MapHeight, 1));

        public static bool TryGetCellBounds(BattlefieldGridConfiguration grid, int cellId, out Bounds bounds)
        {
            bounds = default;
            if (grid == null || cellId < 0 || cellId >= (long)grid.CellsX * grid.CellsY) return false;
            int x = cellId % grid.CellsX, y = cellId / grid.CellsX;
            bounds = new Bounds(new Vector3(
                (float)(grid.MinimumX + (x + .5) * grid.CellWidth),
                (float)(grid.MinimumY + (y + .5) * grid.CellHeight), (float)grid.OriginZ),
                new Vector3((float)grid.CellWidth, (float)grid.CellHeight, 1));
            return true;
        }

        public static bool Frame(Bounds bounds)
        {
            if (!IsFinite(bounds.center) || !IsFinite(bounds.size)) return false;
            SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.Frame(bounds, false);
            view.Focus();
            return true;
        }

        public static string CellEvidence(BattlefieldGridSnapshot grid,int cellId)
        {
            if(grid==null)return "Grid unavailable";
            var text=new StringBuilder();
            if(cellId>=0 && cellId<grid.Cells.Count)
            {
                var cell=grid.Cells[cellId];
                text.AppendLine($"Cell {cellId} ({cell.X},{cell.Y})");
                text.AppendLine("Turret: "+string.Join(", ",cell.TurretIds));
                text.AppendLine("Player: "+string.Join(", ",cell.PlayerIds));
                text.AppendLine("Enemy: "+string.Join(", ",cell.EnemyIds));
            }
            text.AppendLine("Turret OOB: "+string.Join(", ",grid.OutOfBoundsTurretIds));
            text.AppendLine("Player OOB: "+string.Join(", ",grid.OutOfBoundsPlayerIds));
            text.AppendLine("Enemy OOB: "+string.Join(", ",grid.OutOfBoundsEnemyIds));
            return text.ToString();
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
