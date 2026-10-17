// 활성·비파괴 터렛을 들로네 삼각분할한 뒤 같은 지역 Grid에 투영합니다.
// 결과를 계산할 뿐 터렛을 켜거나 잠그지 않으며, 점령 확정은 Runtime 소유자에게 맡깁니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class DynamicRegionCapturePolicy
    {
        private readonly BattlefieldTopologyBuilder _topology = new BattlefieldTopologyBuilder();
        private readonly TriangleGridRasterizer _rasterizer = new TriangleGridRasterizer();

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
                    captured.Add(cell);
            var sorted = new List<int>(captured);
            sorted.Sort();
            return new RegionCaptureEvaluation(definition, sorted, geometry, duplicates);
        }
    }
}
