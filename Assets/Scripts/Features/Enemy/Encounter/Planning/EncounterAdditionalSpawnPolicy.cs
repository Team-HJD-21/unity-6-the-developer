// Planner의 마지막 정책으로 같은 예산의 완성 편성 하나를 내부 지연 출현으로 옮깁니다.
// 한 번의 정적 확보·복원은 급성장으로 세지 않고 예고·위치·정규 출현을 보장할 때만 선택합니다.
// Scene 조회나 실제 생성은 하지 않으며 승인된 예약만 cooldown에 반영합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Enemies.Encounter
{
    public sealed class EncounterAdditionalSpawnPolicy
    {
        private Guid _epoch, _grid;
        private double _nextAllowed;

        public double Remaining(double now) => Math.Max(0, _nextAllowed - now);
        public void Restore(double now, double remaining, Guid epoch, Guid grid)
        {
            if (!double.IsFinite(now) || now < 0 || !double.IsFinite(remaining) || remaining < 0)
                throw new ArgumentOutOfRangeException(nameof(remaining));
            double next = now + remaining;
            if (!double.IsFinite(next) || (remaining > 0 && next <= now))
                throw new ArgumentOutOfRangeException(nameof(remaining));
            _epoch = epoch; _grid = grid; _nextAllowed = next;
        }

        /// <summary>정규 배분/편성 뒤 호출합니다. 추가비용 없이 기존 편성 하나만 떼고 나머지 계획을 유지합니다.</summary>
        public EncounterDeferredSpawn Select(IReadOnlyList<SpawnInstruction> instructions, IReadOnlyList<SpawnArea> areas,
            RegionGridSnapshot region, RegionCaptureTrend trend, EncounterAdditionalSpawnSettings settings,
            int totalBudget, Func<SpawnInstruction, int> cost, out IReadOnlyList<SpawnInstruction> regular, out string reason)
        {
            regular = instructions;
            if (cost == null) throw new ArgumentNullException(nameof(cost));
            if (!CanPlan(region, trend, settings, out reason)) return null;
            if (instructions == null || instructions.Count < 2 || areas == null || areas.Count == 0)
            { reason = "정규 출현/예고 가능한 내부 영역 부족"; return null; }

            SpawnArea internalArea = null;
            foreach (var area in areas)
            {
                if (area == null || !region.Definition.TryGetCellIndex(area.Center, out int cell) ||
                    !region.CapturedCells[cell] || !region.Definition.CapturableCells[cell]) continue;
                var grid = region.Definition.Geometry;
                int x = cell % grid.CellsX, y = cell / grid.CellsX;
                double left = grid.MinimumX + x * grid.CellWidth, bottom = grid.MinimumY + y * grid.CellHeight;
                if (area.Center.X - area.Width / 2 < left || area.Center.X + area.Width / 2 > left + grid.CellWidth ||
                    area.Center.Y - area.Height / 2 < bottom || area.Center.Y + area.Height / 2 > bottom + grid.CellHeight) continue;
                internalArea = area;
                break;
            }
            if (internalArea == null) { reason = "유효한 점유 내부 영역 없음"; return null; }
            int limit = (int)Math.Floor(totalBudget * settings.maximumBudgetShare);
            // 원래 편성/배분 순서를 유지하며 완성 편성을 쪼개지 않습니다.
            for (int i = instructions.Count - 1; i >= 0; i--)
            {
                int selectedCost = cost(instructions[i]);
                if (selectedCost <= 0 || selectedCost > limit) continue;
                var remaining = new List<SpawnInstruction>(instructions.Count - 1);
                for (int j = 0; j < instructions.Count; j++) if (j != i) remaining.Add(instructions[j]);
                var origin = EncounterSpawnOrigin.AtWorld(internalArea);
                var moved = new SpawnInstruction(instructions[i].SquadOrder, "", instructions[i].Enemies,
                    new EncounterSpawnPlacement(origin, internalArea));
                regular = remaining.AsReadOnly();
                reason = $"최근 점유 순증가 {trend.NetRatio:P0}, 확장 {trend.PositiveChanges}회: 기존 예산 중 비용 {selectedCost}/{totalBudget} 내부 출현";
                return new EncounterDeferredSpawn(moved, selectedCost, settings.warningSeconds, reason);
            }
            reason = "예산 몫 안의 완성 편성 없음";
            return null;
        }

        // 이동면 조회 이전에도 같은 조건을 사용해 불필요한 NavMesh 검사를 하지 않습니다.
        public bool CanPlan(RegionGridSnapshot region, RegionCaptureTrend trend, EncounterAdditionalSpawnSettings settings, out string reason)
        {
            reason = "확장 조건 충족";
            if (region == null) { reason = "현재 지역 없음"; return false; }
            if (settings == null || !settings.enabled || !settings.IsValid)
            { reason = "추가 출현 비활성/설정 오류"; return false; }
            if (!trend.HasHistory || trend.PositiveChanges < settings.minimumPositiveChanges ||
                trend.ObservedSeconds < settings.minimumObservedSeconds || trend.NetRatio < settings.minimumNetCaptureRatio)
            { reason = "지속 확장 조건 미달"; return false; }
            if (_epoch != trend.Epoch || _grid != region.GridId)
            { _epoch = trend.Epoch; _grid = region.GridId; _nextAllowed = 0; }
            if (trend.GameSeconds < _nextAllowed)
            { reason = "추가 출현 cooldown"; return false; }
            return true;
        }

        public void RecordQueued(RegionCaptureTrend trend, RegionGridSnapshot region, double cooldown)
        { Restore(trend.GameSeconds, cooldown, trend.Epoch, region.GridId); }
    }
}
