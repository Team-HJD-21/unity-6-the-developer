// 점유 후보 중 시작 셀에서 상하좌우로 이어진 셀만 남깁니다.
// 삼각형과 무관하게 최종 Grid 연결을 판정하며 대각선·행 경계 연결을 허용하지 않습니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public static class RegionGridConnectivity
    {
        /// <summary>지역 정책이 후보를 투영한 뒤 호출합니다. 입력을 변경하지 않고 정렬된 연결 셀을 반환합니다.</summary>
        public static IReadOnlyList<int> RetainConnected(RegionGridDefinition definition,
            IEnumerable<int> candidates, int startCell)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            int count = definition.Geometry.TotalCellCount;
            if (startCell < 0 || startCell >= count) throw new ArgumentOutOfRangeException(nameof(startCell));
            var remaining = new bool[count];
            foreach (int cell in candidates)
            {
                if (cell < 0 || cell >= count) throw new ArgumentOutOfRangeException(nameof(candidates));
                remaining[cell] = true;
            }
            var connected = new List<int>();
            var pending = new Queue<int>();
            Enqueue(startCell, remaining, pending);
            int width = definition.Geometry.CellsX;
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                connected.Add(cell);
                int x = cell % width;
                if (x > 0) Enqueue(cell - 1, remaining, pending);
                if (x + 1 < width) Enqueue(cell + 1, remaining, pending);
                if (cell >= width) Enqueue(cell - width, remaining, pending);
                if (cell + width < count) Enqueue(cell + width, remaining, pending);
            }
            connected.Sort();
            return connected.AsReadOnly();
        }

        private static void Enqueue(int cell, bool[] remaining, Queue<int> pending)
        {
            if (!remaining[cell]) return;
            remaining[cell] = false;
            pending.Enqueue(cell);
        }
    }
}
