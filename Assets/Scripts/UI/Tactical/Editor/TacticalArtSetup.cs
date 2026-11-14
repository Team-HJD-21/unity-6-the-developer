using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

/// <summary>원본 PNG는 보존하고, 전술 UI에 필요한 Sprite rect와 실제 게임 원본의 UI 렌더만 작성합니다.</summary>
public static class TacticalArtSetup
{
    public const string Art="Assets/Art/UI/Tactical";
    private const string Source="Docs/Local/Design/UiProductPolish20261113/";
    private const string FieldScene="Assets/Scenes/Gameplay/Stage_1.unity";
    [MenuItem("Tools/TeamHJD/The Developer/Tactical/Bake approved tactical art")]
    public static void BakeAll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Use clean Edit Mode before baking UI assets.");
        if(!AssetDatabase.IsValidFolder(Art))AssetDatabase.CreateFolder("Assets/Art/UI","Tactical");
        var style=AssetDatabase.LoadAssetAtPath<TacticalHudStyle>(TacticalHudSetup.StylePath);
        if(style==null)throw new InvalidOperationException("Configure the tactical HUD style first.");
        var symbols=Atlas("tactical-symbols-candidate-02.png","TacticalSymbols-v1",new[]{"cannon","missile","enemy_swarm","goal_beacon"});
        style.CannonGlyph=symbols[0]; style.MissileGlyph=symbols[1]; style.EnemyGlyph=symbols[2]; style.GoalGlyph=symbols[3];
        var commands=Atlas("tactical-command-glyphs-candidate-01.png","TacticalCommands-v1",new[]{"power","upgrade","selection","automatic"});
        style.PowerGlyph=commands[0]; style.UpgradeGlyph=commands[1]; style.SelectionGlyph=commands[2]; style.AutomaticGlyph=commands[3];
        BakeTurrets(style); BakeTerrain(style);
        EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
        TacticalHudSetup.Configure();
        Debug.Log("TACTICAL_ART_BAKED");
    }
    [MenuItem("Tools/TeamHJD/The Developer/Tactical/Bake overview terrain only")]
    public static void BakeOverviewTerrain()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save the bound scene in Edit Mode before baking its terrain.");
        var style=AssetDatabase.LoadAssetAtPath<TacticalHudStyle>(TacticalHudSetup.StylePath);
        if(style==null)throw new InvalidOperationException("The authored tactical style is required.");
        BakeTerrain(style); EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
    }
    private static Sprite[] Atlas(string file,string name,string[] ids)
    {
        string source=Source+file,path=Art+"/"+name+".png";
        if(!File.Exists(path))
        {
            if(!File.Exists(source))throw new FileNotFoundException("The saved atlas or its approved source image is required.",path);
            File.Copy(source,path);
        }
        else if(File.Exists(source) && !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(path)))
            throw new InvalidOperationException("Use a new atlas revision; preserve the saved PNG.");
        var texture=Import(path,false);
        var scan=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!scan.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Atlas PNG could not be decoded.");
            var pixels=scan.GetPixels32(); var result=new Sprite[4];
            for(int i=0;i<4;i++)
            {
                int x0=i%2*scan.width/2,y0=(1-i/2)*scan.height/2;
                int left=scan.width,right=-1,bottom=scan.height,top=-1;
                for(int y=y0;y<y0+scan.height/2;y++)for(int x=x0;x<x0+scan.width/2;x++)
                    if(pixels[y*scan.width+x].a>=24){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
                if(right<left || top<bottom)throw new InvalidOperationException("Empty glyph cell: "+ids[i]);
                string spritePath=Art+"/glyph_"+ids[i]+".asset";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if(sprite==null)
                {
                    sprite=Sprite.Create(texture,new Rect(left,bottom,right-left+1,top-bottom+1),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                    sprite.name="tactical_"+ids[i]; AssetDatabase.CreateAsset(sprite,spritePath);
                }
                result[i]=sprite;
            }
            return result;
        }
        finally{Object.DestroyImmediate(scan);}
    }
    private static Texture2D Import(string path,bool singleSprite)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=singleSprite?TextureImporterType.Sprite:TextureImporterType.Default;
        if(singleSprite){importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.spriteBorder=Vector4.zero;}
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.npotScale=TextureImporterNPOTScale.None;
        importer.sRGBTexture=true; importer.alphaIsTransparency=true; importer.isReadable=false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static void BakeTurrets(TacticalHudStyle style)
    {
        var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab").GetComponent<InitialStagePlayerHUD>();
        var catalog=hud.LevelCatalog; var art=new List<TacticalHudStyle.TurretArtEntry>();
        foreach(var definition in AssetDatabase.FindAssets("t:FieldTurretDefinition").Select(g=>AssetDatabase.LoadAssetAtPath<FieldTurretDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(d=>d!=null).OrderBy(d=>d.Id))
        {
            if(!catalog.TryGetPrefab(definition.Id,out var prefab))continue;
            string assetPath=AssetDatabase.GetAssetPath(prefab.gameObject);
            var root=PrefabUtility.LoadPrefabContents(assetPath); Material material=null;
            try
            {
                foreach(var animator in root.GetComponentsInChildren<Animator>(true))
                {
                    var clips=animator.runtimeAnimatorController?.animationClips;
                    var idle=clips?.FirstOrDefault(c=>c!=null && (c.name.ToLowerInvariant().Contains("idle") || c.name.ToLowerInvariant().Contains("off")));
                    if(idle!=null)idle.SampleAnimation(animator.gameObject,0);
                    animator.enabled=false;
                }
                Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default");
                if(shader==null)throw new InvalidOperationException("Unlit sprite shader is missing."); material=new Material(shader);
                Bounds bounds=default; bool any=false;
                foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    string label=(renderer.name+"/"+renderer.sprite?.name).ToLowerInvariant();
                    bool keep=renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite!=null && !label.Contains("range") && !label.Contains("circle") && !label.Contains("minimap") && !label.Contains("flash") && !label.Contains("shadow") && !label.Contains("projectile");
                    renderer.enabled=keep;
                    if(!keep)continue;
                    // Rocket 원본의 앞 프레임이 비발사 상태이며 뒤 프레임에는 불꽃이 들어 있습니다.
                    if(renderer.sprite.texture.name.IndexOf("Rocket",StringComparison.OrdinalIgnoreCase)>=0)
                    {
                        var frames=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer.sprite.texture)).OfType<Sprite>();
                        var rest=frames.OrderBy(s=>s.rect.xMin).ThenBy(s=>s.rect.yMin).FirstOrDefault();
                        if(rest!=null)renderer.sprite=rest;
                    }
                    renderer.gameObject.SetActive(true); renderer.sharedMaterial=material;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(!any || bounds.size.sqrMagnitude<.0001f)throw new InvalidOperationException("No turret body renderers: "+definition.Id);
                float size=Mathf.Max(bounds.size.x,bounds.size.y)*1.12f;
                var region=new Rect((Vector2)bounds.center-Vector2.one*size*.5f,Vector2.one*size);
                string filename=System.Text.RegularExpressions.Regex.Replace(definition.Id,"[^a-zA-Z0-9_.-]","_");
                string path=Art+"/turret_"+filename+".png";
                File.WriteAllBytes(path,Render(root.scene,region,256,256,Color.clear)); Import(path,true);
                art.Add(new TacticalHudStyle.TurretArtEntry{DefinitionId=definition.Id,Detail=AssetDatabase.LoadAssetAtPath<Sprite>(path)});
            }
            finally{if(material!=null)Object.DestroyImmediate(material);PrefabUtility.UnloadPrefabContents(root);}
        }
        style.TurretArt=art.ToArray();
    }
    private static void BakeTerrain(TacticalHudStyle style)
    {
        var original=SceneManager.GetActiveScene(); var source=SceneManager.GetSceneByPath(FieldScene); bool opened=!source.IsValid() || !source.isLoaded;
        if(opened)source=EditorSceneManager.OpenScene(FieldScene,OpenSceneMode.Additive);
        var preview=EditorSceneManager.NewPreviewScene(); Material material=null;
        try
        {
            var binding=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RegionSceneBinding>(true)).Single();
            var geometry=binding.CaptureInitialDefinition().Geometry;
            Rect bounds=Rect.MinMaxRect((float)geometry.MinimumX,(float)geometry.MinimumY,(float)geometry.MaximumX,(float)geometry.MaximumY);
            var controller=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TacticalCommandController>(true)).SingleOrDefault();
            if(controller!=null && controller.TryGetAuthoredCameraBounds(out var authored))bounds=authored;
            Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default");
            if(shader==null)throw new InvalidOperationException("Unlit sprite shader is missing."); material=new Material(shader);
            foreach(var map in source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Tilemap>(true)).Where(m=>m.gameObject.activeInHierarchy))
            {
                var sourceRenderer=map.GetComponent<TilemapRenderer>(); if(sourceRenderer==null || !sourceRenderer.enabled)continue;
                var grid=new GameObject("Overview grid",typeof(Grid)); SceneManager.MoveGameObjectToScene(grid,preview);
                if(map.layoutGrid is Grid sourceGrid)
                {var g=grid.GetComponent<Grid>();g.cellSize=sourceGrid.cellSize;g.cellGap=sourceGrid.cellGap;g.cellLayout=sourceGrid.cellLayout;g.cellSwizzle=sourceGrid.cellSwizzle;}
                var layer=new GameObject(map.name,typeof(Tilemap),typeof(TilemapRenderer)); layer.transform.SetParent(grid.transform,false);
                layer.transform.SetPositionAndRotation(map.transform.position,map.transform.rotation);layer.transform.localScale=map.transform.lossyScale;
                var clone=layer.GetComponent<Tilemap>();clone.tileAnchor=map.tileAnchor;clone.color=map.color;
                var cells=map.cellBounds;clone.SetTilesBlock(cells,map.GetTilesBlock(cells));
                foreach(var cell in cells.allPositionsWithin)if(map.HasTile(cell))
                {clone.SetTileFlags(cell,TileFlags.None);clone.SetColor(cell,map.GetColor(cell));clone.SetTransformMatrix(cell,map.GetTransformMatrix(cell));}
                var renderer=layer.GetComponent<TilemapRenderer>();renderer.sortingLayerID=sourceRenderer.sortingLayerID;renderer.sortingOrder=sourceRenderer.sortingOrder;renderer.sharedMaterial=material;
            }
            int width=512,height=Mathf.Max(1,Mathf.RoundToInt(width*bounds.height/bounds.width));
            string path=Art+"/Stage_1-overview.png";File.WriteAllBytes(path,Render(preview,bounds,width,height,new Color(.035f,.055f,.06f,1)));style.OverviewTerrain=Import(path,false);
            style.TerrainScene=FieldScene;style.TerrainBounds=bounds;
        }
        finally
        {
            if(material!=null)Object.DestroyImmediate(material);EditorSceneManager.ClosePreviewScene(preview);
            if(opened)EditorSceneManager.CloseScene(source,true);if(original.IsValid() && original.isLoaded)SceneManager.SetActiveScene(original);
        }
    }
    private static byte[] Render(Scene scene,Rect world,int width,int height,Color clear)
    {
        var go=new GameObject("Tactical UI asset camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;camera.aspect=world.width/world.height;camera.orthographicSize=world.height*.5f;
        camera.transform.position=new Vector3(world.center.x,world.center.y,-100);camera.nearClipPlane=.01f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=clear;
        camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
        try
        {
            camera.targetTexture=rt;
            if(GraphicsSettings.currentRenderPipeline!=null)
            {
                var request=new RenderPipeline.StandardRequest{destination=rt};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("The active render pipeline cannot bake this camera.");
                RenderPipeline.SubmitRenderRequest(camera,request);
            }
            else camera.Render();
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();return pixels.EncodeToPNG();
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);}
    }
}
