// 지역 Grid의 불변 공간 정의와 정적 지역 소속을 보관합니다.
// 점령 상태와 참가자 밀도는 저장하지 않으며 입력 배열은 방어적으로 복사합니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionGridDefinition
    {
        public BattlefieldGridConfiguration Geometry { get; }
        public IReadOnlyList<int> RegionIds { get; }

        public RegionGridDefinition(BattlefieldGridConfiguration geometry, IEnumerable<int> regionIds = null)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            var values = new List<int>(geometry.TotalCellCount);
            if (regionIds != null)
            {
                foreach (int id in regionIds)
                {
                    if (id < 0) throw new ArgumentOutOfRangeException(nameof(regionIds));
                    if (values.Count == geometry.TotalCellCount)
                        throw new ArgumentException("Region count exceeds grid size.", nameof(regionIds));
                    values.Add(id);
                }
                if (values.Count != geometry.TotalCellCount)
                    throw new ArgumentException("Each grid cell requires one region id.", nameof(regionIds));
            }
            else
            {
                for (int index = 0; index < geometry.TotalCellCount; index++) values.Add(0);
            }
            // 0은 아직 정적 지역에 속하지 않은 셀입니다. 동적 점령도 같은 셀을 사용합니다.
            RegionIds = new ReadOnlyCollection<int>(values);
        }

        public int GetCellIndex(int x, int y)
        {
            if (x < 0 || x >= Geometry.CellsX) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Geometry.CellsY) throw new ArgumentOutOfRangeException(nameof(y));
            return y * Geometry.CellsX + x;
        }

        public bool HasSameDefinition(RegionGridDefinition other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null) return false;
            var a = Geometry; var b = other.Geometry;
            if (a.Origin != b.Origin || a.OriginZ != b.OriginZ || a.MapWidth != b.MapWidth ||
                a.MapHeight != b.MapHeight || a.CellsX != b.CellsX || a.CellsY != b.CellsY) return false;
            for (int i = 0; i < RegionIds.Count; i++) if (RegionIds[i] != other.RegionIds[i]) return false;
            return true;
        }

        public bool TryGetCellIndex(BattlefieldPoint point, out int index)
        {
            if (Geometry.TryMapPoint(point, out int x, out int y))
            {
                index = GetCellIndex(x, y);
                return true;
            }
            index = -1;
            return false;
        }
    }
}
