using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>승인된 Main 장식을 실제 자산·Prefab·Startup에 저장합니다.</summary>
public static class StartupAtmosphereSetup
{
    public const string Art="Assets/Art/UI/MainAtmosphere";
    public const string Prefab="Assets/Prefabs/UI/Common/StartupAtmosphere.prefab";
    [MenuItem("Tools/TeamHJD/The Developer/Main/Apply saved atmosphere")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Preserve the open dirty Scene first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            PlayerSettings.bundleVersion="0.1.0-alpha";
            CommonUiPrefabSetup.ConfigureStartupVersion(scene);
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)!=null)RefineSavedAtmosphere(scene);
            Configure(scene);EditorSceneManager.SaveScene(scene);
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSettings.enterPlayModeOptionsEnabled=true;
            Debug.Log("STARTUP_ATMOSPHERE_SAVED");
        }
        finally{if(setup.Length>0&&setup.All(x=>!string.IsNullOrEmpty(x.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    private static void RefineSavedAtmosphere(Scene scene)
    {
        var background=scene.GetRootGameObjects().Single(r=>r.name=="Canvas").transform.Find("Background").GetComponent<Image>();
        var sprite=background.sprite;var source=sprite.texture;var sr=sprite.rect;
        var rt=RenderTexture.GetTemporary(480,270,0);var old=RenderTexture.active;
        var sample=new Texture2D(480,270,TextureFormat.RGB24,false);var sites=new System.Collections.Generic.List<Vector2>();
        try
        {
            Graphics.Blit(source,rt,new Vector2(sr.width/source.width,sr.height/source.height),new Vector2(sr.x/source.width,sr.y/source.height));
            RenderTexture.active=rt;sample.ReadPixels(new Rect(0,0,480,270),0,0);sample.Apply();
            for(int y=16;y<250;y+=4)for(int x=12;x<468;x+=4)
            {
                var c=sample.GetPixel(x,y);if(c.r*.2126f+c.g*.7152f+c.b*.0722f<.2f)sites.Add(new Vector2(x*4-960,y*4-540));
            }
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(sample);}
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var fx=root.GetComponent<StartupAtmosphere>();fx.Meteor.material=null;fx.MeteorInterval=new Vector2(7,12);fx.StarSites=sites.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void Configure(Scene scene)
    {
        var controls=scene.GetRootGameObjects().Select(r=>r.GetComponent<StartupControlsView>()).Single(x=>x!=null);
        var logo=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).Single(t=>t.name=="LogoText");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)==null){BuildPrefab(logo);RefineSavedAtmosphere(scene);}
        var fx=scene.GetRootGameObjects().Select(r=>r.GetComponent<StartupAtmosphere>()).FirstOrDefault(x=>x!=null);
        if(fx==null)fx=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene)).GetComponent<StartupAtmosphere>();
        var root=scene.GetRootGameObjects().Single(r=>r.GetComponent<StartupSceneRoot>()!=null);
        var credits=root.GetComponent<CreditsSceneLauncher>();
        fx.Controls=controls;fx.Logo=logo;fx.Credits=credits;
        fx.Protected=new[]{logo.rectTransform,(RectTransform)controls.Start.transform,(RectTransform)controls.Settings.transform,(RectTransform)credits.Button.transform,(RectTransform)controls.Exit.transform,controls.VersionLabel.rectTransform};
        root.GetComponent<StartupSceneRoot>().ConfigureAtmosphere(fx);EditorUtility.SetDirty(root.GetComponent<StartupSceneRoot>());
        if(!credits.MenuCanvases.Contains(fx.GetComponent<Canvas>()))credits.MenuCanvases=credits.MenuCanvases.Concat(new[]{fx.GetComponent<Canvas>()}).ToArray();
        EditorUtility.SetDirty(credits);EditorUtility.SetDirty(fx);PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
    }
    private static void BuildPrefab(TMP_Text logo)
    {
        string path=Art+"/MeteorSheet.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.isReadable=true;
        var rects=new SpriteMetaData[8];
        for(int i=0;i<8;i++)rects[i]=new SpriteMetaData{name="Meteor_"+i,rect=new Rect(i%4*443,(1-i/4)*443,443,443),alignment=9,pivot=Vector2.one*.5f};
#pragma warning disable 618
        importer.spritesheet=rects;
#pragma warning restore 618
        importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        if(sprites.Length!=8)throw new InvalidOperationException("Expected 8 meteor frames.");
        var texture=sprites[0].texture;var pixels=texture.GetPixels32();var ink=new Vector4(1,1,0,0);
        foreach(var sprite in sprites)
        {
            var r=sprite.rect;
            for(int y=0;y<r.height;y++)for(int x=0;x<r.width;x++)if(pixels[((int)r.y+y)*texture.width+(int)r.x+x].a>=20)
            {ink.x=Mathf.Min(ink.x,x/r.width);ink.y=Mathf.Min(ink.y,y/r.height);ink.z=Mathf.Max(ink.z,(x+1)/r.width);ink.w=Mathf.Max(ink.w,(y+1)/r.height);}
        }
        importer.isReadable=false;importer.SaveAndReimport();
        sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        var shine=AssetDatabase.LoadAssetAtPath<Material>(Art+"/MainLogoShine.mat");
        if(shine==null){shine=new Material(logo.fontSharedMaterial);shine.name="MainLogoShine";shine.shader=Shader.Find("TheDeveloper/UI/MainLogoShine");AssetDatabase.CreateAsset(shine,Art+"/MainLogoShine.mat");}
        if(ShaderUtil.GetShaderMessages(shine.shader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("Logo shine shader error.");
        var root=new GameObject("Startup Atmosphere",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup),typeof(StartupAtmosphere));
        try
        {
            root.layer=5;var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=group.interactable=false;
            var fx=root.GetComponent<StartupAtmosphere>();fx.Frame=Rect("Reference Frame",root.transform,1920,1080);fx.LogoShine=shine;fx.MeteorFrames=sprites;fx.MeteorInk=ink;
            var meteor=Rect("Shooting Star",fx.Frame,140,140);fx.Meteor=meteor.gameObject.AddComponent<Image>();fx.Meteor.sprite=sprites[0];fx.Meteor.raycastTarget=false;fx.Meteor.enabled=false;
            fx.Stars=new CanvasGroup[2];
            for(int i=0;i<2;i++)
            {
                var star=Rect("Twinkle "+i,fx.Frame,10,10);var fade=star.gameObject.AddComponent<CanvasGroup>();fade.alpha=0;fade.blocksRaycasts=fade.interactable=false;fx.Stars[i]=fade;
                Pixel(star,6+i*2,2,.43f);Pixel(star,2,6+i*2,.43f);Pixel(star,2,2,1);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{Object.DestroyImmediate(root);}
    }
    private static RectTransform Rect(string name,Transform parent,float width,float height)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=new Vector2(width,height);return r;
    }
    private static void Pixel(Transform parent,float width,float height,float alpha)
    {
        var r=Rect("Pixel",parent,width,height);var im=r.gameObject.AddComponent<Image>();im.color=new Color(.91f,.89f,.79f,alpha);im.raycastTarget=false;
    }
}
