// 지도와 Field의 공용 점유 외곽선 및 바닥 표시 리소스를 검증합니다.
// 공유 변·구멍·대각선 접촉·월드 원점과 전용 정렬 순서를 검사합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;

public sealed class FieldTerritoryTests
{
    private static RegionGridSnapshot Snapshot(int columns, int rows, params int[] captured)
    {
        var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, columns * 10, rows * 10, columns, rows);
        return RegionGridSnapshot.FromCapturedCells(new MatchId("boundary-test"), Guid.NewGuid(), 1,
            RegionPolicyMode.Static, new RegionGridDefinition(geometry), captured);
    }

    [Test]
    public void AdjacentCapturedCellsHaveNoInternalBoundaryAndInwardAlwaysPointsToCapturedGround()
    {
        var snapshot = Snapshot(3, 2, 0, 1);
        var edges = RegionBoundaryGeometry.Build(snapshot);
        Assert.That(edges.Count, Is.EqualTo(6));
        foreach (var edge in edges)
        {
            var midpoint = (edge.Start + edge.End) * .5f;
            var inside = midpoint + edge.Inward * .1f;
            var outside = midpoint - edge.Inward * .1f;
            Assert.That(snapshot.TryIsCapturedAt(new BattlefieldPoint(inside.x, inside.y), out bool captured) && captured, Is.True);
            snapshot.TryIsCapturedAt(new BattlefieldPoint(outside.x, outside.y), out captured);
            Assert.That(captured, Is.False);
        }
    }

    [Test]
    public void HoleAndDiagonalContactKeepSeparateClosedLoops()
    {
        Assert.That(CountLoops(RegionBoundaryGeometry.Build(Snapshot(3, 3, 0, 1, 2, 3, 5, 6, 7, 8))), Is.EqualTo(2));
        Assert.That(CountLoops(RegionBoundaryGeometry.Build(Snapshot(2, 2, 0, 3))), Is.EqualTo(2));
        Assert.That(RegionBoundaryGeometry.Build(null), Is.Empty);
        Assert.That(RegionBoundaryGeometry.Build(Snapshot(2, 2)), Is.Empty);
    }

    [Test]
    public void EveryThreeByThreeCapturePatternClosesWithoutDuplicateEdgesAndPreservesCapturedArea()
    {
        for (int mask = 0; mask < 512; mask++)
        {
            int[] cells = Enumerable.Range(0, 9).Where(i => (mask & (1 << i)) != 0).ToArray();
            var edges = RegionBoundaryGeometry.Build(Snapshot(3, 3, cells));
            CountLoops(edges);
            Assert.That(new HashSet<(Vector2, Vector2)>(edges.Select(e => (e.Start, e.End))).Count, Is.EqualTo(edges.Count), "mask=" + mask);
            double area = edges.Sum(e => ((double)e.Start.x * e.End.y - (double)e.End.x * e.Start.y) * .5);
            Assert.That(area, Is.EqualTo(cells.Length * 100).Within(.001), "mask=" + mask);
        }
    }

    [Test]
    public void OffsetOriginAndRectangularCellsUseExactSharedWorldCoordinates()
    {
        var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(100, -20), 9, 60, 20, 3, 2);
        var snapshot = RegionGridSnapshot.FromCapturedCells(new MatchId("offset"), Guid.NewGuid(), 1,
            RegionPolicyMode.Static, new RegionGridDefinition(grid), new[] { 0 });
        var edges = RegionBoundaryGeometry.Build(snapshot);
        var corners = edges.Select(e => e.Start).ToArray();
        Assert.That(corners, Is.EquivalentTo(new[] { new Vector2(70, -30), new Vector2(90, -30), new Vector2(90, -20), new Vector2(70, -20) }));
    }

    [Test]
    public void AuthoredGroundMaterialAndSortingLayerKeepTerrainBelowAndWorldObjectsAbove()
    {
        Assert.That(SortingLayer.NameToID(FieldTerritoryRenderer.SortingLayerName), Is.Not.EqualTo(0));
        int layer = SortingLayer.GetLayerValueFromName(FieldTerritoryRenderer.SortingLayerName);
        Assert.That(layer, Is.GreaterThan(SortingLayer.GetLayerValueFromName("Map")));
        Assert.That(layer, Is.LessThan(SortingLayer.GetLayerValueFromName("Default")));
        Assert.That(layer, Is.LessThan(SortingLayer.GetLayerValueFromName("Entities")));
        var material = Resources.Load<Material>("Core/FieldTerritory");
        Assert.That(material, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
        var inside = material.GetColor("_InsideColor");
        var outside = material.GetColor("_OutsideColor");
        Assert.That(inside.g, Is.GreaterThan(inside.r));
        Assert.That(outside.r, Is.GreaterThan(outside.g));
        Assert.That(inside.a, Is.InRange(.01f, .5f));
        Assert.That(outside.a, Is.InRange(.01f, .5f));
    }

    private static int CountLoops(IReadOnlyList<RegionBoundarySegment> edges)
    {
        int loops = 0, index = 0;
        while (index < edges.Count)
        {
            Vector2 start = edges[index].Start, cursor = start;
            do
            {
                Assert.That(index, Is.LessThan(edges.Count), "A contour must close before the segment list ends.");
                Assert.That(edges[index].Start, Is.EqualTo(cursor), "Each contour must be continuous.");
                cursor = edges[index++].End;
            } while (cursor != start);
            loops++;
        }
        return loops;
    }
}
