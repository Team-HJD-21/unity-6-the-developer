// 실제 전투 관측을 정적 확보 규칙에 넣고 확정 셀·해금만 Match 원본에 반영합니다.
// 게임 규칙은 StaticRegionCapture, Scene 조회/피해 구독은 이 조립 계층이 맡습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.AI;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class RegionSceneBinding
    {
        [SerializeField, Min(1)] private float captureHoldSeconds = 60;
        [SerializeField, Min(1)] private float captureAbandonSeconds = 180;
        [SerializeField, Min(1)] private float captureLossSeconds = 30;
        [SerializeField, Min(1)] private float captureMinimumDamage = 20;
        [SerializeField, Min(0)] private int captureEnemiesPerParticipant = 2;
        private readonly SortedDictionary<int, StaticRegionCapture> _staticCapture = new();
        private readonly List<IEnemyCombatSource> _combatSources = new();
        public IReadOnlyList<StaticRegionCaptureSnapshot> StaticCaptureProgress =>
            _staticCapture.Values.Select(state => state.Snapshot).ToArray();

        public StaticRegionCaptureSettings CaptureSettings => new StaticRegionCaptureSettings(captureHoldSeconds,
            captureAbandonSeconds, captureLossSeconds, captureMinimumDamage, captureEnemiesPerParticipant);

        /// <summary>Game Debugger가 시험값을 명시 적용합니다. 현재 진행 초는 보존하고 다음 관측부터 판정합니다.</summary>
        public void ConfigureCapture(StaticRegionCaptureSettings settings)
        {
            if (!HasWriteAuthority || settings == null) throw new InvalidOperationException("Only the current Host may tune capture.");
            if (new[] { settings.HoldSeconds, settings.AbandonSeconds, settings.LossSeconds, settings.MinimumTeamDamage }
                    .Any(value => value > float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(settings));
            captureHoldSeconds = (float)settings.HoldSeconds; captureAbandonSeconds = (float)settings.AbandonSeconds;
            captureLossSeconds = (float)settings.LossSeconds; captureMinimumDamage = (float)settings.MinimumTeamDamage;
            captureEnemiesPerParticipant = settings.EnemiesPerParticipant;
        }

        public void BindCombatSources(IEnumerable<IEnemyCombatSource> sources)
        {
            ReleaseCombatSources();
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            foreach (var source in sources.Distinct())
                if (source != null) { source.DamageObserved += OnEnemyDamage; _combatSources.Add(source); }
        }

        private void ReleaseCombatSources()
        {
            foreach (var source in _combatSources) source.DamageObserved -= OnEnemyDamage;
            _combatSources.Clear();
        }

        private void OnEnemyDamage(EnemyDamageObservation damage)
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Static ||
                damage.MatchId != _source.Snapshot.MatchId ||
                (damage.Contributor != EnemyDamageContributor.Player && damage.Contributor != EnemyDamageContributor.Turret) ||
                !_definition.TryGetCellIndex(damage.Position, out int cell)) return;
            int region = _definition.RegionIds[cell];
            if (_source.Snapshot.IsRegionUnlocked(region) && _staticCapture.TryGetValue(region, out var progress))
                progress.RecordDamage(damage.Damage);
        }

        private void ResetStaticCapture(RegionGridDefinition definition)
        {
            _staticCapture.Clear();
            foreach (int id in definition.RegionIds.Distinct().Where(id => id > 0))
                if (Enumerable.Range(0, definition.RegionIds.Count).Any(i => definition.RegionIds[i] == id && definition.CapturableCells[i]))
                    _staticCapture.Add(id, new StaticRegionCapture(id));
        }

        private IReadOnlyList<int> InitialControlUnitCells(RegionGridDefinition definition)
        {
            if (!definition.TryGetCellIndex(ControlUnitPosition(), out int cell) || !definition.CapturableCells[cell])
                throw new InvalidOperationException("CU must be inside one capturable land cell.");
            return new[] { cell };
        }

        private void InitializeStaticCapture()
        {
            _session.UnlockRegionsThrough(_source.Snapshot, 1);
            _session.UpdateRegionCapture(_source.Snapshot, InitialControlUnitCells(_definition), RegionCaptureUpdateMode.Replace);
            ResetStaticCapture(_definition);
        }

        /// <summary>Scene의 실제 생존 위치 표본 후 호출합니다. 참가자 수는 타이머 배속으로 사용하지 않습니다.</summary>
        public void TickStaticCapture(double gameSeconds, IReadOnlyList<PlayerSpatialInput> livingPlayers,
            IReadOnlyList<EnemySpatialInput> livingEnemies)
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Static) return;
            if (_source.Snapshot.UnlockedRegionIds.Count == 0) InitializeStaticCapture();
            var participants = new Dictionary<int, int>();
            var enemies = new Dictionary<int, int>();
            foreach (var player in livingPlayers) CountRegion(player.Position, participants);
            foreach (var enemy in livingEnemies) CountRegion(enemy.Position, enemies);
            var settings = CaptureSettings;
            foreach (var progress in _staticCapture.Values)
            {
                participants.TryGetValue(progress.RegionId, out int allies);
                enemies.TryGetValue(progress.RegionId, out int pressure);
                bool changed = progress.Tick(gameSeconds, _source.Snapshot.IsRegionUnlocked(progress.RegionId), allies, pressure, settings);
                if (changed)
                {
                    var snapshot = _source.Snapshot;
                    var cells = new HashSet<int>(Enumerable.Range(0, snapshot.CapturedCells.Count).Where(i => snapshot.CapturedCells[i]));
                    for (int i = 0; i < _definition.RegionIds.Count; i++)
                        if (_definition.RegionIds[i] == progress.RegionId && _definition.CapturableCells[i])
                        {
                            if (progress.IsSecured) cells.Add(i);
                            else cells.Remove(i);
                        }
                    foreach (int cu in InitialControlUnitCells(_definition)) cells.Add(cu);
                    _session.UpdateRegionCapture(_source.Snapshot, cells, RegionCaptureUpdateMode.Replace);
                }
                if (progress.IsSecured)
                {
                    // 물만 있는 빈 구역은 건너뛰고 다음 실제 점유 대상 구역을 엽니다.
                    int next = _staticCapture.Keys.FirstOrDefault(id => id > progress.RegionId);
                    if (next > 0 && !_source.Snapshot.IsRegionUnlocked(next))
                        _session.UnlockRegionsThrough(_source.Snapshot, next);
                }
            }
        }

        private void CountRegion(BattlefieldPoint position, Dictionary<int, int> counts)
        {
            if (!_definition.TryGetCellIndex(position, out int cell)) return;
            int region = _definition.RegionIds[cell];
            if (region <= 0) return;
            counts.TryGetValue(region, out int count); counts[region] = count + 1;
        }

        private RegionGridDefinition WithWalkableMask(RegionGridDefinition definition)
        {
            // 편집기 단위 구성 검사는 NavMesh 없이도 값을 만들 수 있습니다. 실제 Match는 지형을 반드시 확인합니다.
            if (!UnityEngine.Application.isPlaying) return WithCaptureExclusions(definition);
            var mesh = NavMesh.CalculateTriangulation();
            if (mesh.indices.Length == 0) throw new InvalidOperationException("Load the Stage NavMesh before preparing its capture grid.");
            var mask = new bool[definition.Geometry.TotalCellCount];
            var rasterizer = new TriangleGridRasterizer();
            for (int i = 0; i < mesh.indices.Length; i += 3)
            {
                var a = mesh.vertices[mesh.indices[i]]; var b = mesh.vertices[mesh.indices[i + 1]];
                var c = mesh.vertices[mesh.indices[i + 2]];
                foreach (int cell in rasterizer.Rasterize(new BattlefieldPoint(a.x, a.y),
                        new BattlefieldPoint(b.x, b.y), new BattlefieldPoint(c.x, c.y), definition.Geometry)) mask[cell] = true;
            }
            if (!mask.Any(value => value)) throw new InvalidOperationException("The Stage has no capturable land inside its grid.");
            return WithCaptureExclusions(new RegionGridDefinition(definition.Geometry, definition.RegionIds, mask));
        }

        // 지형의 통행 가능성과 별도로 지정 진입로 등을 점유 분모에서 뺍니다. 선/점 접촉만으로 이웃 셀을 제외하지 않습니다.
        private RegionGridDefinition WithCaptureExclusions(RegionGridDefinition definition)
        {
            if (_captureExcludedAreas == null || _captureExcludedAreas.Length == 0) return definition;
            var mask = definition.CapturableCells.ToArray();
            var grid = definition.Geometry;
            foreach (var area in _captureExcludedAreas)
            {
                if (!float.IsFinite(area.center.x) || !float.IsFinite(area.center.y) ||
                    !float.IsFinite(area.size.x) || !float.IsFinite(area.size.y) || area.size.x <= 0 || area.size.y <= 0)
                    throw new InvalidOperationException("Capture exclusion requires a finite positive world XY rectangle.");
                for (int y = 0; y < grid.CellsY; y++)
                    for (int x = 0; x < grid.CellsX; x++)
                    {
                        double left = grid.MinimumX + x * grid.CellWidth, bottom = grid.MinimumY + y * grid.CellHeight;
                        if (area.max.x > left && area.min.x < left + grid.CellWidth &&
                            area.max.y > bottom && area.min.y < bottom + grid.CellHeight) mask[y * grid.CellsX + x] = false;
                    }
            }
            if (!mask.Any(value => value)) throw new InvalidOperationException("Capture exclusions removed every eligible cell.");
            return new RegionGridDefinition(grid, definition.RegionIds, mask);
        }
    }
}
