// 활성·비파괴 터렛을 들로네 삼각분할한 뒤 같은 지역 Grid에 투영합니다.
// 결과를 계산할 뿐 터렛을 켜거나 잠그지 않으며, 점령 확정은 Runtime 소유자에게 맡깁니다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamHJD.Game.Domain
{
    public sealed class DynamicRegionCapturePolicy
    {
        public const string ControlUnitNodeId = "core/control-unit";
        private readonly BattlefieldTopologyBuilder _topology = new BattlefieldTopologyBuilder();
        private readonly TriangleGridRasterizer _rasterizer = new TriangleGridRasterizer();

        /// <summary>기하 진단/독립 시험용 투영입니다. 실제 동적 점유에는 CU를 받는 EvaluateConnected를 사용합니다.</summary>
        public RegionCaptureEvaluation Evaluate(RegionGridDefinition definition, IEnumerable<RegionTurretInput> inputs)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var active = new List<RegionTurretInput>();
            var ids = new HashSet<EntityId>();
            foreach (RegionTurretInput input in inputs)
            {
                if (input == null) throw new ArgumentException("Turret input cannot be null.", nameof(inputs));
                if (!ids.Add(input.EntityId)) throw new ArgumentException("Duplicate turret id.", nameof(inputs));
                if (input.IsActivated && !input.IsDestroyed) active.Add(input);
            }
            active.Sort((left, right) => StringComparer.Ordinal.Compare(left.EntityId.Value, right.EntityId.Value));

            var positions = new HashSet<BattlefieldPoint>();
            var points = new List<TurretSpatialInput>();
            int duplicates = 0;
            foreach (RegionTurretInput turret in active)
            {
                if (!positions.Add(turret.Position)) { duplicates++; continue; }
                points.Add(new TurretSpatialInput(turret.EntityId, turret.Position));
            }

            BattlefieldSpatialSnapshot geometry = _topology.Build(new BattlefieldSpatialInput(points));
            var vertices = new Dictionary<EntityId, BattlefieldPoint>();
            foreach (BattlefieldVertex vertex in geometry.Vertices) vertices.Add(vertex.EntityId, vertex.Position);
            var captured = new HashSet<int>();
            foreach (BattlefieldTriangle triangle in geometry.Triangles)
                foreach (int cell in _rasterizer.Rasterize(vertices[triangle.VertexA], vertices[triangle.VertexB],
                             vertices[triangle.VertexC], definition.Geometry))
                    if (definition.CapturableCells[cell]) captured.Add(cell);
            var sorted = new List<int>(captured);
            sorted.Sort();
            return new RegionCaptureEvaluation(definition, sorted, geometry, duplicates);
        }

        /// <summary>Host 지역 바인딩이 CU와 활성 터렛으로 실제 점유 후보를 계산할 때 호출합니다.</summary>
        /// <param name="initialCells">CU 셀을 포함한 연결된 시작 셀 집합. null이면 CU 셀 하나를 사용합니다.</param>
        /// <remarks>셀 범위는 현재 Grid 기준입니다. 물/이동 불가 셀의 제작 마스크는 별도 지역 정의 계약에서 제공합니다.</remarks>
        public RegionCaptureEvaluation EvaluateConnected(RegionGridDefinition definition,
            IEnumerable<RegionTurretInput> inputs, BattlefieldPoint controlUnitPosition,
            IEnumerable<int> initialCells = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (!definition.TryGetCellIndex(controlUnitPosition, out int start))
                throw new ArgumentException("CU must be inside the region grid.", nameof(controlUnitPosition));
            var initial = initialCells == null ? new HashSet<int> { start } : new HashSet<int>(initialCells);
            if (initial.Any(cell => cell < 0 || cell >= definition.CapturableCells.Count))
                throw new ArgumentOutOfRangeException(nameof(initialCells));
            if (initial.Any(cell => !definition.CapturableCells[cell]))
                throw new ArgumentException("Initial cells must be capturable land.", nameof(initialCells));
            if (!initial.Contains(start))
                throw new ArgumentException("Initial cells must include the CU cell.", nameof(initialCells));
            if (RegionGridConnectivity.RetainConnected(definition, initial, start).Count != initial.Count)
                throw new ArgumentException("Initial cells must be connected to the CU by cardinal edges.", nameof(initialCells));

            var nodes = new List<RegionTurretInput>();
            foreach (RegionTurretInput input in inputs)
            {
                if (input == null) throw new ArgumentException("A spatial input is required.", nameof(inputs));
                if (input.EntityId.Value == ControlUnitNodeId)
                    throw new ArgumentException("The CU spatial identity is reserved.", nameof(inputs));
                nodes.Add(input);
            }
            // CU는 위치 노드일 뿐이며 터렛의 활성·체력·전력 상태를 대신 소유하지 않습니다.
            nodes.Add(new RegionTurretInput(new EntityId(ControlUnitNodeId), controlUnitPosition, true, false));
            RegionCaptureEvaluation projected = Evaluate(definition, nodes);
            var candidates = new HashSet<int>(projected.CellIndices);
            candidates.UnionWith(initial);
            var connected = new List<int>(RegionGridConnectivity.RetainConnected(definition, candidates, start));
            return new RegionCaptureEvaluation(definition, connected, projected.Geometry,
                projected.DuplicatePositionCount, initial.Count, candidates.Count - connected.Count);
        }
    }
}
