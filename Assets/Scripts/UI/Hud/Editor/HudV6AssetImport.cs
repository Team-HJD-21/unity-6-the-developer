// 승인된 소스 PNG의 import·분할·피벗과 독립 폰트 에셋을 제작합니다. 기존 NEXON SDF는 수정하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class HudV6AssetImport
{
    public const string Folder = "Assets/Art/UI/HudV6";
    public const string StylePath = Folder + "/CombatHudStyle.asset";
    [Serializable] private sealed class Manifest { public Entry[] assets; }
    [Serializable] private sealed class Entry { public string id; public string source; public float[] border_lbrt; public float[] optical_content_bbox; }

    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Import approved assets")]
    public static void Import()
    {
        var source = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/import-source.json"));
        var style = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(StylePath);
        if (style == null) { style = ScriptableObject.CreateInstance<CombatHudStyle>(); AssetDatabase.CreateAsset(style, StylePath); }
        var sprites = new List<CombatHudStyle.SpriteEntry>();
        foreach (var entry in source.assets)
        {
            string path = string.IsNullOrEmpty(entry.source) ? Folder + "/" + entry.id + ".png" : entry.source;
            AssetDatabase.ImportAsset(path);
            if (string.IsNullOrEmpty(entry.source))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = entry.id == "label_territory_ko" ? FilterMode.Point : FilterMode.Bilinear;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                if (entry.id == "equipped_needle_long")
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    // Source manifest y is top-down; Unity pivot is measured from the bottom.
                    settings.spritePivot = new Vector2(.5f, 5f / 176f);
                }
                importer.SetTextureSettings(settings);
                var b = entry.border_lbrt;
                importer.spriteBorder = b != null && b.Length == 4 ? new Vector4(b[0], b[1], b[2], b[3]) : Vector4.zero;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("HUD sprite unavailable: " + path);
            if (entry.id == "fill_white")
            {
                // Filled gauges must map 0..1 to the full rect, without the source's soft transparent edge.
                // Reuse only the opaque central texels; leave the approved texture and its importer intact.
                string fillPath = Folder + "/fill_white_solid.asset";
                var fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fillPath);
                if (fillSprite == null)
                {
                    fillSprite = Sprite.Create(sprite.texture, new Rect(10, 10, 4, 4), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    fillSprite.name = "fill_white_solid";
                    AssetDatabase.CreateAsset(fillSprite, fillPath);
                }
                sprite = fillSprite;
            }
            if (!string.IsNullOrEmpty(entry.source) && entry.optical_content_bbox?.Length == 4)
            {
                // Crop only UI sprite UV metadata. Original PNG/importer/GUID and gameplay sprites remain unchanged.
                string uiPath = Folder + "/" + entry.id + "_ui.asset";
                var uiSprite = AssetDatabase.LoadAssetAtPath<Sprite>(uiPath);
                if (uiSprite == null)
                {
                    var b = entry.optical_content_bbox;
                    uiSprite = Sprite.Create(sprite.texture, new Rect(b[0], sprite.texture.height - b[3], b[2] - b[0], b[3] - b[1]), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    uiSprite.name = entry.id + "_ui";
                    AssetDatabase.CreateAsset(uiSprite, uiPath);
                }
                sprite = uiSprite;
            }
            sprites.Add(new CombatHudStyle.SpriteEntry { Id = entry.id, Sprite = sprite });
        }
        style.Sprites = sprites.ToArray();
        style.HeaderFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/GravityBold8 SDF.asset");
        style.LabelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/GravityRegular5 SDF.asset");
        string bodyPath = Folder + "/CombatHudBodySDF.asset";
        style.BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(bodyPath);
        if (style.BodyFont == null)
        {
            var raw = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NEXON Lv1 Gothic OTF.otf");
            var font = TMP_FontAsset.CreateFontAsset(raw);
            font.name = "CombatHudBodySDF";
            font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font, bodyPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            style.BodyFont = font;
        }
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        Debug.Log("HUD_V6_ASSETS_IMPORTED " + sprites.Count);
    }
}
