using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Other painted tiles are connected ground; only empty cells form the water edge.</summary>
[CreateAssetMenu(fileName = "Stage1WaterEdgeRuleTile", menuName = "Tiles/Stage1 Water Edge Rule Tile")]
public sealed class Stage1WaterEdgeRuleTile : RuleTile
{
    public override bool RuleMatch(int neighbor, TileBase other)
    {
        if (neighbor == TilingRuleOutput.Neighbor.This)
            return other != null;

        if (neighbor == TilingRuleOutput.Neighbor.NotThis)
            return other == null;

        return base.RuleMatch(neighbor, other);
    }
}
