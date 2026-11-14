using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Content;
using UnityEngine;

[CreateAssetMenu(menuName = "The Developer/UI/Tactical HUD")]
public sealed class TacticalHudStyle : ScriptableObject
{
    [System.Serializable] public struct TurretArtEntry { public string DefinitionId; public Sprite Detail; }
    public CombatHudStyle Common;
    public TacticalHudView ViewPrefab;
    public FieldTurretUpgradeDefinition[] LowPower, Firepower;
    [Header("Authored tactical art")]
    public Sprite CannonGlyph, MissileGlyph, EnemyGlyph, GoalGlyph;
    public Sprite PowerGlyph, UpgradeGlyph, SelectionGlyph, AutomaticGlyph;
    public TurretArtEntry[] TurretArt;
    public Texture2D OverviewTerrain;
    public string TerrainScene;
    public Rect TerrainBounds;
    [Min(1)] public float GlyphEnterZoom = 2.5f;
    [Min(1)] public float GlyphLeaveZoom = 2.2f;
    [Min(12)] public float GlyphSize = 32;
    public Sprite DetailFor(string definitionId)
    {
        if (TurretArt != null) foreach (var entry in TurretArt) if (entry.DefinitionId == definitionId) return entry.Detail;
        return null;
    }
    public Sprite GlyphFor(string definitionId)
    {
        string id = definitionId.ToLowerInvariant();
        if (id.Contains("missile")) return MissileGlyph;
        if (id.Contains("canon") || id.Contains("cannon")) return CannonGlyph;
        return DetailFor(definitionId);
    }
    [Min(1)] public float EntryZoom = 2.2f;
    [Min(1)] public float MaximumZoom = 8;
    [Min(.1f)] public float MinimumZoom = .7f;
    [Min(.01f)] public float ZoomStep = 1.2f;
    [Header("Camera motion")]
    [Min(0)] public float CameraMoveSeconds = .18f;
    [Min(0)] public float CameraZoomSeconds = .16f;
    [Min(.1f)] public float PanSpeed = 1.1f;
    [Min(0)] public float ShotQuietSeconds = 2;
    [Min(0)] public float HoverDelay = .22f;
    [Min(0)] public float HitHoldSeconds = 5;
    [Min(10)] public float MinimumMapSpan = 80;
    [Range(8, 64)] public int MaximumEnemyGroups = 24;
    public Color Active = new(.48f, .68f, .39f), Inactive = new(.62f, .65f, .61f);
    public Color Territory = new(.72f, .75f, .67f, .55f);
    public Color Enemy = new(.84f, .38f, .31f);
}
