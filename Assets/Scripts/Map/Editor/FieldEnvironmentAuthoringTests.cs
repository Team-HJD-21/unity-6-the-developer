using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Map;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public sealed class FieldEnvironmentAuthoringTests
{
    [Test]
    public void Stage1ResolvesFieldComponentsAndTheirSceneReferences()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay/Stage_1.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var occlusion = roots.SelectMany(root => root.GetComponentsInChildren<FieldPlayerOcclusion>(true)).Single();
            var shadows = roots.SelectMany(root => root.GetComponentsInChildren<FieldTileShadows>(true)).Single();

            using var occlusionFields = new SerializedObject(occlusion);
            using var shadowFields = new SerializedObject(shadows);
            Assert.That(occlusionFields.FindProperty("_cameraFollow").objectReferenceValue, Is.InstanceOf<MatchCameraFollow>());
            Assert.That(shadowFields.FindProperty("_tilemap").objectReferenceValue, Is.SameAs(occlusion.GetComponent<Tilemap>()));
            Assert.That(shadowFields.FindProperty("_sun").objectReferenceValue, Is.InstanceOf<Light2D>());
            Assert.That(shadowFields.FindProperty("_timeSource").objectReferenceValue, Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void AdditionalOccluderAndBuiltinTilesFadeWhileOtherSpritesStayOpaque()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var generated = new List<Object>();
        try
        {
            var grid = new GameObject("Grid", typeof(Grid));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(grid, scene);
            var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tiles, scene);
            tiles.transform.SetParent(grid.transform, false);
            var tilemap = tiles.GetComponent<Tilemap>();
            var texture = new Texture2D(8, 8);
            generated.Add(texture);

            Sprite Place(int x, string name)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8f, 0, SpriteMeshType.FullRect);
                generated.Add(sprite);
                sprite.name = name;
                var tile = ScriptableObject.CreateInstance<Tile>();
                generated.Add(tile);
                tile.sprite = sprite;
                tilemap.SetTile(new Vector3Int(x, 0), tile);
                return sprite;
            }

            Place(0, "Overworld_Trees_test");
            Place(1, "Overworld_Objects_22");
            Sprite extra = Place(2, "NewScenery");
            Place(3, "NewScenery");

            var effect = tiles.AddComponent<FieldPlayerOcclusion>();
            using var fields = new SerializedObject(effect);
            var additional = fields.FindProperty("_additionalOccluders");
            additional.arraySize = 1;
            additional.GetArrayElementAtIndex(0).objectReferenceValue = extra;
            fields.ApplyModifiedPropertiesWithoutUndo();
            const BindingFlags methods = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FieldPlayerOcclusion).GetMethod("Awake", methods).Invoke(effect, null);
            typeof(FieldPlayerOcclusion).GetMethod("CollectOccludingTiles", methods)
                .Invoke(effect, new object[] { new Vector2(2f, .5f), new Vector2(2f, .5f), 1f });
            typeof(FieldPlayerOcclusion).GetMethod("UpdateFades", methods).Invoke(effect, new object[] { 1f });

            for (int x = 0; x < 3; x++)
                Assert.That(tilemap.GetColor(new Vector3Int(x, 0)).a, Is.EqualTo(.23f).Within(.01f));
            Assert.That(tilemap.GetColor(new Vector3Int(3, 0)).a, Is.EqualTo(1f));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = generated.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(generated[i]);
        }
    }
}
