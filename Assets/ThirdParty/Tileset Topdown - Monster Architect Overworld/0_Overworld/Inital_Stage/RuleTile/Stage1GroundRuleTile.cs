using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Connects a ground tier to itself and to higher tiers on the same Tilemap.
/// </summary>
[CreateAssetMenu(fileName = "Stage1GroundRuleTile", menuName = "Tiles/Stage1 Ground Rule Tile")]
public sealed class Stage1GroundRuleTile : RuleTile
{
    public override bool RuleMatch(int neighbor, TileBase other)
    {
        if (neighbor == TilingRuleOutput.Neighbor.This || neighbor == TilingRuleOutput.Neighbor.NotThis)
        {
            if (other is RuleOverrideTile overrideTile)
                other = overrideTile.m_InstanceTile;

            bool connected = other is Stage1GroundRuleTile ground &&
                ground.IsPeachFamily == IsPeachFamily && ground.TerrainLevel >= TerrainLevel;

            return neighbor == TilingRuleOutput.Neighbor.This ? connected : !connected;
        }

        return base.RuleMatch(neighbor, other);
    }

    // Peach and gray occupy the top and bottom bands of Overworld_Ground.png.
    // They connect to each other, separately from the green/khaki/brown family.
    private bool IsPeachFamily => m_DefaultSprite != null &&
        (m_DefaultSprite.rect.y >= 320f || m_DefaultSprite.rect.y < 160f);

    // Green, khaki, and brown occupy adjacent 96-pixel-wide columns.
    // RuleOverrideTile replaces m_DefaultSprite on its instance.
    private int TerrainLevel
    {
        get
        {
            if (m_DefaultSprite == null)
                return 0;

            if (IsPeachFamily)
                return m_DefaultSprite.rect.y < 160f ? 2 : 1;

            float x = m_DefaultSprite.rect.x;
            return x >= 192f ? 3 : x >= 96f ? 2 : 1;
        }
    }
}
