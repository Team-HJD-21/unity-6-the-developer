using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>편집 가능한 시각 Prefab과 실제 additive Credits Scene을 제작합니다. 원본 아트/폰트는 저장하지 않습니다.</summary>
public static class CreditsSceneSetup
{
    public const string Art = "Assets/Art/Cinematics/Credits";
    public const string Prefab = "Assets/Prefabs/UI/Cinematics/CreditsCinematic.prefab";
    private const int Layer = 30;
    private static Material _sprite, _particles, _hotParticles;
    private static TMP_FontAsset _font, _mono, _logo;
    private static readonly string[] Names = { "양현석", "이영빈", "김진태", "황재동", "조수빈" };
    private static readonly string[] Roles = { "Project Lead / Core Systems & UI Architect", "Player, Cooperative UX & Cinematic Director", "World, Mission & Agent Experience Designer", "Defense, Progression & Balance Designer", "Enemy, Encounter & AI Platform Engineer" };
    private static readonly int[] ChapterOwners = { 0, 2, 4, 3, 1 };

    [MenuItem("Tools/TeamHJD/The Developer/Credits/Build cinematic scene and connect Main")]
    public static void Build() => Build(true);

    [MenuItem("Tools/TeamHJD/The Developer/Credits/Rebuild cinematic only")]
    public static void BuildCinematic() => Build(false);

    private static void Build(bool connectMain)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        for (int i=0;i<SceneManager.sceneCount;i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the open dirty Scene first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory("Assets/Scenes/Presentation"); Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
        Import("ShipSide",new Rect(320,140,1532,431),new Vector2(.5f,.5f),100,FilterMode.Point);
        Import("AstronautFront",new Rect(253,143,628,1060),new Vector2(.5f,0),100,FilterMode.Bilinear);
        Import("AstronautLookRight",new Rect(259,141,611,1059),new Vector2(.5f,0),100,FilterMode.Bilinear);
        Import("AstronautWelding",new Rect(228,128,620,1068),new Vector2(.5f,0),100,FilterMode.Bilinear);
        Import("HullWorkPanel",new Rect(69,242,2035,232),new Vector2(.5f,.5f),100,FilterMode.Bilinear);
        Import("Drone",new Rect(163,145,1096,849),new Vector2(.5f,.5f),100,FilterMode.Bilinear);
        Import("EngineFlame",new Rect(527,156,1147,370),new Vector2(.97f,.5f),600,FilterMode.Point);
        AssetDatabase.Refresh();
        _sprite=Material("SpriteMaterial",Shader.Find("TheDeveloper/Credits/Sprite"));
        _particles=Material("ParticleMaterial",Shader.Find("TheDeveloper/Credits/SoftBillboard"));
        _hotParticles=Material("HotParticleMaterial",Shader.Find("TheDeveloper/Credits/HotParticle"));
        _hotParticles.mainTexture=Sprite("Assets/Art/Sprites/Turrets/TurretRangeCircleSmooth.png").texture;
        EditorUtility.SetDirty(_hotParticles);AssetDatabase.SaveAssetIfDirty(_hotParticles);
        _particles.mainTexture=Sprite("Assets/Art/Sprites/Turrets/TurretRangeCircleSmooth.png").texture;
        EditorUtility.SetDirty(_particles);AssetDatabase.SaveAssetIfDirty(_particles);
        _font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/NEXON Lv1 Gothic OTF SDF.asset");
        _mono=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/JetBrainsMono/JetBrainsMono-Medium SDF.asset");
        _logo=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/GravityBold8 SDF.asset");
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var root=Obj("Credits Cinematic",null);var director=root.AddComponent<CreditsCinematicDirector>();director.LogoSeconds=4;director.ChapterFade=.55f;
            var camera=Obj("Cinematic Camera",root.transform).AddComponent<Camera>();camera.orthographic=true;camera.depth=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<Layer;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.transform.position=new Vector3(0,0,-50);director.Camera=camera;
            var overlay=Canvas("Film frame and typography",root.transform,camera,1000);
            var top=Rect("Top letterbox",overlay,1920,84,0,498);top.gameObject.AddComponent<Image>().color=Color.black;
            var bottom=Rect("Bottom letterbox",overlay,1920,84,0,-498);bottom.gameObject.AddComponent<Image>().color=Color.black;
            director.Chapters=new CreditsCinematicChapter[5];
            for(int i=0;i<5;i++)
            {
                var group=Obj((i+1).ToString("00")+" "+((CreditsChapterKind)i),root.transform);var c=group.AddComponent<CreditsCinematicChapter>();c.Kind=(CreditsChapterKind)i;c.Duration=new[]{9.5f,10f,10f,10f,11.5f}[i];director.Chapters[i]=c;
                var captionRect=Rect("Role",overlay,1720,54,0,-498);captionRect.SetParent(group.transform,true);
                // 캡션은 각 Chapter가 켜질 때만 보이는 별도의 camera-space Canvas입니다.
                var captionCanvas=Canvas("Role caption",group.transform,camera,1001);Object.DestroyImmediate(captionRect.gameObject);
                c.RoleGroup=Group(captionCanvas);Label("Role",captionCanvas,Roles[ChapterOwners[i]],_mono,23,1720,60,0,-498).color=new Color(.77f,.83f,.82f);
                Space(group.transform,i);
            }
            Weld(director.Chapters[0]);Flight(director.Chapters[1]);Asteroids(director.Chapters[2]);Drop(director.Chapters[3]);Drone(director.Chapters[4]);
            var fade=Rect("Chapter fade to black",overlay,1920,1080);fade.gameObject.AddComponent<Image>().color=Color.black;director.Black=Group(fade);
            var logo=Rect("The Developer original logo",overlay,1700,300);director.Logo=Group(logo);director.LogoContent=logo;
            Label("Original logo type",logo,"The Developer",_logo,72,1680,220,0,0).color=new Color(.89f,.91f,.85f);
            var final=Rect("Static credits only",overlay,1920,1080);director.FinalCredits=Group(final);
            Label("Credits",final,"CREDITS",_mono,27,1000,60,0,380);Label("Team",final,"Team HJD",_mono,22,1000,48,0,305).color=new Color(.62f,.69f,.68f);
            var finalRoles=new[]{"Project Lead\nCore Systems & UI Architect","Player, Cooperative UX\n& Cinematic Director","World, Mission &\nAgent Experience Designer","Defense, Progression\n& Balance Designer","Enemy, Encounter\n& AI Platform Engineer"};
            for(int i=0;i<5;i++)
            {
                var role=Label("Role "+i,final,finalRoles[i],_mono,21,660,72,-340,175-i*88);role.alignment=TextAlignmentOptions.Right;role.color=new Color(.61f,.69f,.68f);
                var name=Label("Name "+i,final,Names[i],_font,39,400,68,250,175-i*88);name.alignment=TextAlignmentOptions.Left;
            }
            var display=Rect("Cinema viewport",root.transform,1920,1080);
            var displayCanvas=display.gameObject.AddComponent<Canvas>();displayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;displayCanvas.sortingOrder=20000;
            var background=Rect("Full screen black matte",display,0,0);background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.offsetMin=background.offsetMax=Vector2.zero;background.gameObject.AddComponent<Image>().color=Color.black;
            var picture=Rect("Live 16 by 9 picture",display,1600,900);director.Display=picture.gameObject.AddComponent<RawImage>();director.Display.raycastTarget=false;
            var fit=picture.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9;
            FrontendSoundtrackSetup.ConfigureCredits(director);
            director.EvaluateAt(0,true);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
            EditorSceneManager.SaveScene(scene,CreditsSceneLauncher.ScenePath);EditorSceneManager.CloseScene(scene,true);
            var builds=EditorBuildSettings.scenes.ToList();if(!builds.Any(s=>s.path==CreditsSceneLauncher.ScenePath))builds.Add(new EditorBuildSettingsScene(CreditsSceneLauncher.ScenePath,true));else builds.First(s=>s.path==CreditsSceneLauncher.ScenePath).enabled=true;EditorBuildSettings.scenes=builds.ToArray();
            if(connectMain){var startup=EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");ConnectMain(startup);EditorSceneManager.SaveScene(startup);}
        }
        finally { if(setup.Length>0&&setup.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("CREDITS_CINEMATIC_AUTHORED");
    }
    public static void ConnectMain(Scene scene)
    {
        var controls=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StartupControlsView>(true)).Single();
        var list=controls.Content;
        var credits=list.Find("Credits")?.GetComponent<Button>();
        if(credits==null){credits=Object.Instantiate(controls.Settings,list,false);credits.name="Credits";credits.GetComponent<UiButtonFeedback>().Label.text="CREDITS";}
        var all=new[]{controls.Start,controls.Settings,credits,controls.Exit};
        for(int i=0;i<all.Length;i++)
        {
            ((RectTransform)all[i].transform).anchoredPosition=new Vector2(0,160-i*110);
            all[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=all[(i+3)%4],selectOnDown=all[(i+1)%4]};
            PrefabUtility.RecordPrefabInstancePropertyModifications(all[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(all[i].transform);
        }
        var root=scene.GetRootGameObjects().Single(r=>r.GetComponent<StartupSceneRoot>()!=null);
        var launcher=root.GetComponent<CreditsSceneLauncher>();if(launcher==null)launcher=root.AddComponent<CreditsSceneLauncher>();
        launcher.Button=credits;launcher.MenuCanvases=scene.GetRootGameObjects().Select(r=>r.GetComponent<Canvas>()).Where(c=>c!=null && c.name!="Credits Transition").ToArray();
        var existing=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Credits Transition");if(existing!=null)Object.DestroyImmediate(existing);
        var transition=Rect("Credits Transition",null,1920,1080);SceneManager.MoveGameObjectToScene(transition.gameObject,scene);
        var canvas=transition.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var scaler=transition.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var fill=Rect("Black",transition,0,0);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;fill.gameObject.AddComponent<Image>().color=Color.black;
        transition.gameObject.AddComponent<GraphicRaycaster>();launcher.Transition=Group(transition);launcher.Transition.alpha=0;launcher.Transition.blocksRaycasts=false;
        root.GetComponent<StartupSceneRoot>().ConfigureCredits(launcher);EditorUtility.SetDirty(root.GetComponent<StartupSceneRoot>());EditorUtility.SetDirty(launcher);
        FrontendSoundtrackSetup.ConfigureMenu(scene);
    }
    private static void Import(string name,Rect rect,Vector2 pivot,float ppu,FilterMode filter)
    {
        string path=Art+"/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=ppu;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.filterMode=filter;
#pragma warning disable 618
        importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=rect,alignment=9,pivot=pivot}};
#pragma warning restore 618
        importer.SaveAndReimport();
    }
    private static Material Material(string name,Shader shader)
    {
        if(shader==null)throw new InvalidOperationException("Missing cinematic shader");string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else if(m.shader!=shader){m.shader=shader;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}return m;
    }
    private static GameObject Obj(string name,Transform parent)
    { var go=new GameObject(name);go.layer=Layer;if(parent!=null)go.transform.SetParent(parent,false);return go; }
    private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    private static Sprite[] Frames(string clip)
    {var c=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Player/WeaponPosesV2/"+clip+".anim");var b=AnimationUtility.GetObjectReferenceCurveBindings(c).First(x=>x.path==""&&x.propertyName=="m_Sprite");return AnimationUtility.GetObjectReferenceCurve(c,b).Select(x=>(Sprite)x.value).ToArray();}
    private static SpriteRenderer Picture(string name,Transform parent,Sprite sprite,int order,float width=0)
    {
        var r=Obj(name,parent).AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=_sprite;r.sortingOrder=order;if(width>0)r.transform.localScale=Vector3.one*(width/sprite.bounds.size.x);return r;
    }
    private static RectTransform Rect(string name,Transform parent,float width,float height,float x=0,float y=0)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=Layer;var r=(RectTransform)go.transform;if(parent!=null)r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=new Vector2(width,height);r.anchoredPosition=new Vector2(x,y);return r;}
    private static RectTransform Canvas(string name,Transform parent,Camera camera,int sorting)
    {
        var r=Rect(name,parent,1920,1080);var c=r.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=.5f;c.sortingOrder=sorting;
        var s=r.gameObject.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;return r;
    }
    private static CanvasGroup Group(Component c) {var g=c.gameObject.GetComponent<CanvasGroup>();return g!=null?g:c.gameObject.AddComponent<CanvasGroup>();}
    private static TMP_Text Label(string name,Transform parent,string value,TMP_FontAsset font,float size,float w,float h,float x=0,float y=0)
    {var r=Rect(name,parent,w,h,x,y);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=value;t.textWrappingMode=TextWrappingModes.NoWrap;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.87f,.91f,.87f);t.raycastTarget=false;return t;}
    private static CreditsNameReveal Name(CreditsCinematicChapter c,int index,Vector3 position,float scale= .012f)
    {
        index=ChapterOwners[index];
        var canvas=Rect("Name in scene",c.transform,400,100);canvas.localPosition=position;canvas.localScale=Vector3.one*scale;
        var cv=canvas.gameObject.AddComponent<Canvas>();cv.renderMode=RenderMode.WorldSpace;cv.overrideSorting=true;cv.sortingOrder=60;
        c.NameGroup=Group(canvas);var t=Label("Name",canvas,Names[index],_font,58,400,100);t.ForceMeshUpdate();
        var reveal=t.gameObject.AddComponent<CreditsNameReveal>();reveal.Text=t;
        c.NameReveal=reveal;return reveal;
    }
    private static void Space(Transform parent,int seed)
    {
        var sky=Picture("Original space",parent,Sprite("Assets/Art/UI/Images/Space Background.png"),-100,55);sky.color=new Color(.24f,.3f,.38f);
        var rng=new System.Random(771+seed);
        for(int i=0;i<12;i++){var r=Picture("Distant rock "+i,parent,Sprite("Assets/ThirdParty/MINING_TOWN/ASTEROID/ASTEROID/ASTEROID_2_96.png"),-50,.2f+(float)rng.NextDouble()*.5f);r.transform.localPosition=new Vector3((float)rng.NextDouble()*40-20,(float)rng.NextDouble()*22-11,0);r.color=new Color(.25f,.3f,.34f);}
    }
    private static CreditsCinematicChapter.Effect Effect(Transform parent,string name,float start,float end,Color color,float rate,float speed,float size,float life,bool burst=false)
    {
        var ps=Obj(name,parent).AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=83;
        var main=ps.main;main.playOnAwake=false;main.useUnscaledTime=true;main.loop=false;main.duration=Mathf.Max(.1f,end-start);main.startLifetime=life;main.startSpeed=speed;main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size);main.startColor=color;main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=burst?0:rate;if(burst)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)rate)});
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.08f;shape.arc=360;
        var fade=ps.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(0,1)});fade.color=g;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_particles;renderer.sortingOrder=75;
        return new CreditsCinematicChapter.Effect{System=ps,Start=start,End=end};
    }
    private static CreditsCinematicChapter.Sound Sound(Transform parent,string name,string clip,float start,float end,float volume,float pitch=1,bool loop=false)
    {
        var source=Obj(name,parent).AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/"+clip+".wav");
        if(source.clip==null)throw new InvalidOperationException("Missing existing SFX: "+clip);
        source.playOnAwake=false;source.loop=loop;source.spatialBlend=0;source.volume=0;source.pitch=pitch;
        return new CreditsCinematicChapter.Sound{Source=source,Start=start,End=end,Volume=volume};
    }
    private static void Wipe(CreditsNameReveal reveal)
    {
        var parent=(RectTransform)reveal.Text.transform.parent;var clip=Rect("Wake reveals name",parent,400,100,-200,0);clip.pivot=new Vector2(0,.5f);clip.gameObject.AddComponent<RectMask2D>();
        reveal.Text.transform.SetParent(clip,false);var text=reveal.Text.rectTransform;text.anchorMin=text.anchorMax=new Vector2(0,.5f);text.anchoredPosition=new Vector2(200,0);
        reveal.RevealClip=clip;reveal.RevealWidth=400;
    }
    private static void Weld(CreditsCinematicChapter c)
    {
        c.CameraFrom=new Vector2(-.15f,.2f);c.CameraTo=new Vector2(.1f,1.4f);c.ZoomFrom=2.45f;c.ZoomTo=3.9f;c.RollFrom=0;c.RollTo=-2;c.CaptionStart=5.1f;
        var hull=Picture("Original hull",c.transform,Sprite("Assets/Art/Sprites/Spaceship/Spaceship_by_HSM/Idle/TopDown_Spaceship_c2_Idle_HSM.png"),10);hull.transform.localScale=Vector3.one*3.4f;hull.transform.localPosition=new Vector3(.07f,1.65f,0);c.Vehicle=hull;
        var engraving=Name(c,0,new Vector3(0,1.65f,0),.012f);
        var etched=AssetDatabase.LoadAssetAtPath<Material>(Art+"/EngravedName.mat");if(etched==null){etched=new Material(_font.material);etched.name="Engraved name";etched.SetColor("_FaceColor",new Color(.10f,.19f,.24f));etched.EnableKeyword("UNDERLAY_ON");etched.SetColor("_UnderlayColor",new Color(.65f,.8f,.85f,.55f));etched.SetFloat("_UnderlayOffsetX",-.35f);etched.SetFloat("_UnderlayOffsetY",.35f);AssetDatabase.CreateAsset(etched,Art+"/EngravedName.mat");}engraving.Text.fontSharedMaterial=etched;
        c.HeroRig=Obj("Astronaut holding welding torch",c.transform).transform;c.HeroFrames=new[]{Sprite(Art+"/AstronautWelding.png")};c.Hero=Picture("Welding pose with actual grip",c.HeroRig,c.HeroFrames[0],40,2.35f);c.HeroVisor=new Vector4(.44f,.59f,.31f,.13f);
        c.WeldNozzle=Obj("Copper nozzle endpoint",c.HeroRig).transform;c.WeldNozzle.localPosition=new Vector3(-1.016f,.785f,0);
        var metal=Sprite(Art+"/HullWorkPanel.png");c.Workplate=Picture("Continuous hull workpiece",c.transform,metal,50,12);c.Workplate.transform.localPosition=new Vector3(-1.016f,-2.184f,0);c.Workplate.color=new Color(.72f,.78f,.82f);
        var arc=GameObject.CreatePrimitive(PrimitiveType.Quad);arc.name="Small irregular welding arc";arc.layer=Layer;arc.transform.SetParent(c.transform,false);Object.DestroyImmediate(arc.GetComponent<Collider>());arc.transform.localScale=Vector3.one*.55f;c.WeldingArc=arc.GetComponent<MeshRenderer>();c.WeldingArc.sharedMaterial=Material("WeldingArcMaterial",Shader.Find("TheDeveloper/Credits/WeldingArc"));c.WeldingArc.sortingOrder=76;
        var sparks=Effect(c.transform,"Short white hot metal spray",.65f,3.35f,new Color(1,.9f,.72f),92,0,.014f,.35f);
        var main=sparks.System.main;main.gravityModifier=.7f;main.startSpeed=new ParticleSystem.MinMaxCurve(2.2f,3.5f);main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.4f);
        var shape=sparks.System.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=48;shape.radius=.012f;sparks.System.transform.localRotation=Quaternion.Euler(-65,-50,0);
        var renderer=sparks.System.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_hotParticles;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=.2f;renderer.velocityScale=.018f;
        var cooling=new Gradient();cooling.SetKeys(new[]{new GradientColorKey(new Color(1,1,.92f),0),new GradientColorKey(new Color(1,.42f,.1f),.45f),new GradientColorKey(new Color(.35f,.08f,.015f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.2f),new GradientAlphaKey(0,1)});var colors=sparks.System.colorOverLifetime;colors.color=cooling;
        var longSparks=Effect(c.transform,"Sparse ballistic sparks",.65f,3.35f,new Color(1,.6f,.25f),13,4,.008f,.5f);var longMain=longSparks.System.main;longMain.gravityModifier=1;longMain.startSpeed=new ParticleSystem.MinMaxCurve(2.8f,4.8f);longMain.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.55f);var longShape=longSparks.System.shape;longShape.shapeType=ParticleSystemShapeType.Cone;longShape.angle=65;longShape.radius=.02f;longSparks.System.transform.localRotation=Quaternion.Euler(-65,-40,0);var longRenderer=longSparks.System.GetComponent<ParticleSystemRenderer>();longRenderer.sharedMaterial=_hotParticles;longRenderer.renderMode=ParticleSystemRenderMode.Stretch;longRenderer.lengthScale=.24f;longRenderer.velocityScale=.025f;var longColors=longSparks.System.colorOverLifetime;longColors.color=cooling;
        var smoke=Effect(c.transform,"Small weld vapor",.65f,3.35f,new Color(.44f,.52f,.59f,.13f),8,.12f,.16f,.8f);var smokeShape=smoke.System.shape;smokeShape.radius=.025f;var smokeVelocity=smoke.System.velocityOverLifetime;smokeVelocity.enabled=true;smokeVelocity.x=-.12f;smokeVelocity.y=.4f;smokeVelocity.z=0;var smokeSize=smoke.System.sizeOverLifetime;smokeSize.enabled=true;smokeSize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,2.5f));smoke.System.GetComponent<ParticleSystemRenderer>().sortingOrder=42;
        var embers=Effect(c.transform,"Cooling orange specks",.65f,3.35f,new Color(1,.3f,.05f),8,.4f,.013f,.65f);var emberMain=embers.System.main;emberMain.gravityModifier=.3f;embers.System.GetComponent<ParticleSystemRenderer>().sharedMaterial=_hotParticles;
        c.Effects=new[]{sparks,longSparks,smoke,embers};
        c.Sounds=new[]{Sound(c.transform,"Welding contact texture","PlayerMine",.65f,3.35f,.055f,1.15f,true)};
    }
    private static void Flight(CreditsCinematicChapter c)
    {
        c.CameraFrom=new Vector2(-4,0);c.CameraTo=Vector2.zero;c.ZoomFrom=2.4f;c.ZoomTo=6.1f;c.RollFrom=-2;c.RollTo=0;
        c.VehicleRig=Obj("Side flight rig",c.transform).transform;c.Vehicle=Picture("Same ship side profile",c.VehicleRig,Sprite(Art+"/ShipSide.png"),40,10.7f);
        var flames=new List<SpriteRenderer>();
        foreach(float y in new[]{.88f,-.67f}){var f=Picture("Pulsing blue engine",c.VehicleRig,Sprite(Art+"/EngineFlame.png"),39);f.transform.localPosition=new Vector3(-5.1f,y,0);flames.Add(f);}
        c.Flames=flames.ToArray();var name=Name(c,1,new Vector3(0,.2f,0),.022f);Wipe(name);c.CaptionStart=6.3f;
        var effects=new List<CreditsCinematicChapter.Effect>();
        foreach(float y in new[]{.88f,-.67f})
        {
            var exhaust=Effect(c.VehicleRig,"Engine nozzle exhaust "+y,.3f,6.3f,new Color(.3f,.68f,1),28,.8f,.018f,.4f);exhaust.System.transform.localPosition=new Vector3(-5.1f,y,0);exhaust.System.transform.localRotation=Quaternion.Euler(0,-90,0);
            var cone=exhaust.System.shape;cone.shapeType=ParticleSystemShapeType.Cone;cone.angle=12;cone.radius=.04f;
            var renderer=exhaust.System.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_hotParticles;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=.35f;renderer.velocityScale=.03f;
            effects.Add(exhaust);
        }
        var stars=Effect(c.transform,"Flight star streaks",.4f,6.3f,new Color(.55f,.73f,.8f),14,0,.013f,1.5f);var shape=stars.System.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(24,12,.01f);
        var starVelocity=stars.System.velocityOverLifetime;starVelocity.enabled=true;starVelocity.x=-9;starVelocity.y=0;starVelocity.z=0;var sr=stars.System.GetComponent<ParticleSystemRenderer>();sr.renderMode=ParticleSystemRenderMode.Stretch;sr.lengthScale=.4f;sr.velocityScale=.03f;sr.sortingOrder=-40;
        effects.Add(stars);
        var wake=Effect(c.transform,"Acceleration wake reveals name",5.95f,6.95f,new Color(.42f,.63f,.74f,.25f),30,2,.014f,.55f);wake.System.transform.localRotation=Quaternion.Euler(0,-90,0);var wakeShape=wake.System.shape;wakeShape.shapeType=ParticleSystemShapeType.Cone;wakeShape.angle=10;wakeShape.radius=.06f;var wakeRenderer=wake.System.GetComponent<ParticleSystemRenderer>();wakeRenderer.sharedMaterial=_hotParticles;wakeRenderer.renderMode=ParticleSystemRenderMode.Stretch;wakeRenderer.lengthScale=.25f;wakeRenderer.velocityScale=.025f;wakeRenderer.sortingOrder=55;effects.Add(wake);c.Effects=effects.ToArray();
        c.Sounds=new[]{Sound(c.transform,"Engine bed","SpaceShipHover",.2f,6.8f,.075f,1,true),Sound(c.transform,"Acceleration pass","SpaceShipPassing",4.5f,7.2f,.11f,1.1f)};
    }
    private static void Asteroids(CreditsCinematicChapter c)
    {
        c.CameraFrom=new Vector2(-3.5f,-.1f);c.CameraTo=new Vector2(2,.65f);c.ZoomFrom=6.4f;c.ZoomTo=5.7f;c.RollFrom=-3;c.RollTo=1.5f;c.CaptionStart=6.1f;
        Name(c,2,new Vector3(1.2f,.65f,0),.022f);const int count=84;var rng=new System.Random(503);
        c.Props=new Transform[count];c.PropStart=new Vector3[count];c.PropVelocity=new Vector3[count];c.PropSpin=new float[count];
        var variants=new[]{"ASTEROID_2_96","ASTEROID_3_96","ASTEROID_2_2_96","ASTEROID_3_2_96"};
        for(int i=0;i<count;i++)
        {
            int depth=i%3;float size=(.25f+(float)rng.NextDouble()*.65f)*(1+depth*.45f);
            var r=Picture("Asteroid cluster "+i,c.transform,Sprite("Assets/ThirdParty/MINING_TOWN/ASTEROID/ASTEROID/"+variants[i%4]+".png"),15+depth*10,size);c.Props[i]=r.transform;
            float x=(i%4<2?-6:7)+(float)(rng.NextDouble()+rng.NextDouble()-1)*10;
            float y=(i%2==0?3.8f:-3.2f)+(float)(rng.NextDouble()-.5)*3+.09f*x;
            c.PropStart[i]=new Vector3(x,y,0);c.PropVelocity[i]=new Vector3(-.08f-depth*.08f,.008f*(i%5-2),0);c.PropSpin[i]=(i%2==0?1:-1)*(1+depth*1.5f);
            r.transform.localPosition=c.PropStart[i];r.color=Color.Lerp(new Color(.3f,.38f,.43f),new Color(.8f,.84f,.82f),depth*.4f);
        }
        c.Ambient=new Transform[3];c.AmbientStart=new[]{new Vector3(12,-.1f,0),new Vector3(12,-5,0),new Vector3(-11,5,0)};c.AmbientVelocity=new[]{new Vector3(-2.8f,.15f,0),new Vector3(-.7f,.03f,0),new Vector3(-.6f,-.03f,0)};
        for(int i=0;i<3;i++){var rock=Picture("Foreground parallax rock "+i,c.transform,Sprite("Assets/ThirdParty/MINING_TOWN/ASTEROID/ASTEROID/"+variants[i]+".png"),65,5.2f+i*.4f);rock.color=new Color(.34f,.4f,.43f);c.Ambient[i]=rock.transform;rock.transform.localPosition=c.AmbientStart[i];}
        c.Effects=new[]{Effect(c.transform,"Slow fine cosmic debris",0,9.4f,new Color(.45f,.55f,.6f),5,.12f,.024f,1.3f)};
        ConfigureAsteroidVisitor(c);
    }
    public static void ConfigureAsteroidVisitor(CreditsCinematicChapter c)
    {
        c.BackgroundSlimeFrames=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/Enemies/Slime/Slime_Idle.png").OfType<Sprite>()
            .Where(s=>new[]{"Slime_Idle_0","Slime_Idle_1","Slime_Idle_2","Slime_Idle_3"}.Contains(s.name)).OrderBy(s=>s.name).ToArray();
        if(c.BackgroundSlime==null)c.BackgroundSlime=Obj("Distant drifting slime",c.transform).AddComponent<SpriteRenderer>();
        var slime=c.BackgroundSlime;slime.sprite=c.BackgroundSlimeFrames[0];
        slime.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/SpriteMaterial.mat");
        slime.sortingOrder=10;slime.color=new Color(.55f,.65f,.72f,.9f);
        // 원본 128px 셀의 투명 여백을 유지하며 실제 몸체는 약 0.6 world unit으로 표시한다.
        slime.transform.localScale=Vector3.one*(6.2f/slime.sprite.bounds.size.x);
        slime.transform.localPosition=new Vector3(c.SlimeFrom.x,c.SlimeFrom.y,0);slime.enabled=false;
    }
    private static Sprite Slice(string name,string path,Rect rect,float ppu)
    {
        string asset=Art+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(asset);if(existing!=null)return existing;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var sprite=UnityEngine.Sprite.Create(texture,rect,Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);sprite.name=name;AssetDatabase.CreateAsset(sprite,asset);return sprite;
    }
    private static void Drop(CreditsCinematicChapter c)
    {
        c.CameraFrom=new Vector2(0,2);c.CameraTo=new Vector2(.15f,0);c.ZoomFrom=6.3f;c.ZoomTo=3.5f;
        var tile=Slice("FieldGroundTile","Assets/Art/Sprites/Turrets/ground_shaker_asset/Terrains/terrain.png",new Rect(0,384,128,128),32);
        var ground=Picture("Actual field terrain",c.transform,tile,-20);ground.drawMode=SpriteDrawMode.Tiled;ground.size=new Vector2(60,36);ground.color=new Color(.35f,.43f,.4f);
        c.Ground=ground;
        var road=Picture("Field service strip",c.transform,Slice("FieldServiceStrip","Assets/Art/Sprites/Turrets/ground_shaker_asset/Terrains/terrain.png",new Rect(400,0,96,128),32),-19);road.drawMode=SpriteDrawMode.Tiled;road.size=new Vector2(3,36);road.color=new Color(.37f,.42f,.42f);
        c.GroundLayers=new[]{ground,road};
        var roadRect=road.sprite.rect;var roadBlock=new MaterialPropertyBlock();roadBlock.SetVector("_UvClamp",new Vector4((roadRect.x+.5f)/road.sprite.texture.width,(roadRect.y+.5f)/road.sprite.texture.height,(roadRect.xMax-.5f)/road.sprite.texture.width,(roadRect.yMax-.5f)/road.sprite.texture.height));road.SetPropertyBlock(roadBlock);
        var sampling=new MaterialPropertyBlock();sampling.SetVector("_UvClamp",new Vector4(.5f/tile.texture.width,384.5f/tile.texture.height,127.5f/tile.texture.width,511.5f/tile.texture.height));ground.SetPropertyBlock(sampling);
        var baseSprite=Slice("TurretBase","Assets/Art/Sprites/Turrets/ground_shaker_asset/Blue/Towers/towers_walls_blank.png",new Rect(0,512,128,128),32);
        var gunSprite=Slice("TurretGun","Assets/Art/Sprites/Turrets/ground_shaker_asset/Blue/Weapons/turret_02_mk3.png",new Rect(0,0,128,128),32);
        c.Props=new Transform[3];c.PropStart=new Vector3[3];c.PropEnd=new[]{new Vector3(0,-1.6f,0),new Vector3(-3.2f,1,0),new Vector3(3.1f,1.3f,0)};c.PropScale=new Vector3[3];c.Effects=new CreditsCinematicChapter.Effect[3];c.Flames=new SpriteRenderer[3];c.Shadows=new SpriteRenderer[3];c.ShadowScale=new Vector3[3];c.CaptionStart=6.3f;
        var sounds=new List<CreditsCinematicChapter.Sound>();
        for(int i=0;i<3;i++)
        {
            var rig=Obj("Air drop turret "+i,c.transform).transform;Picture("Existing turret base",rig,baseSprite,30,2.15f);Picture("Existing turret weapon",rig,gunSprite,31,2.0f);
            var brake=Picture("Short braking exhaust",rig,Sprite(Art+"/EngineFlame.png"),29);brake.transform.localPosition=new Vector3(0,-.8f,0);brake.transform.localRotation=Quaternion.Euler(0,0,90);c.Flames[i]=brake;
            c.Props[i]=rig;c.PropStart[i]=c.PropEnd[i]+Vector3.up*(10+i);c.PropScale[i]=Vector3.one;
            var shadow=Picture("Ground contact shadow "+i,c.transform,Sprite("Assets/Art/Sprites/Turrets/TurretRangeCircleSmooth.png"),-10,2.8f);shadow.sharedMaterial=_particles;shadow.transform.position=c.PropEnd[i]+Vector3.down*.1f;shadow.transform.localScale=Vector3.Scale(shadow.transform.localScale,new Vector3(1,.45f,1));c.Shadows[i]=shadow;c.ShadowScale[i]=shadow.transform.localScale;
            float landing=2.4f+i*.85f;
            var dust=Effect(c.transform,"Landing dust "+i,landing,landing+.12f,new Color(.62f,.57f,.42f),22,2.2f,.22f,.65f,true);var main=dust.System.main;main.startSize=new ParticleSystem.MinMaxCurve(.16f,.32f);main.startLifetime=new ParticleSystem.MinMaxCurve(.4f,.7f);var shape=dust.System.shape;shape.radius=.55f;
            var size=dust.System.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,1.8f));c.Effects[i]=dust;
            sounds.Add(Sound(c.transform,"Landing mechanism "+i,"TurretOn",landing,landing+.65f,.07f,.85f));
        }
        c.Sounds=sounds.ToArray();
        var name=Name(c,3,new Vector3(2.8f,-1.55f,0),.014f);name.Text.color=new Color(.8f,.85f,.79f);name.transform.parent.name="Name appears beside landed turret";
    }
    private static void Drone(CreditsCinematicChapter c)
    {
        c.CameraFrom=new Vector2(0,.3f);c.CameraTo=new Vector2(2.06f,.72f);c.ZoomFrom=3.4f;c.ZoomTo=1.05f;c.CaptionStart=7;
        c.HeroRig=Obj("Player front and glance",c.transform).transform;c.HeroFrames=new[]{Sprite(Art+"/AstronautFront.png")};c.LookFrames=new[]{Sprite(Art+"/AstronautLookRight.png")};c.Hero=Picture("Cinematic front astronaut",c.HeroRig,c.HeroFrames[0],40,1.8f);c.HeroVisor=new Vector4(.5f,.60f,.32f,.12f);
        c.DroneRig=Obj("Friendly maintenance drone",c.transform).transform;c.Drone=Picture("Robot shell",c.DroneRig,Sprite(Art+"/Drone.png"),45,1.9f);
        var name=Name(c,4,new Vector3(0,.05f,0),.00475f);var nameCanvas=name.transform.parent;nameCanvas.SetParent(c.DroneRig,false);nameCanvas.localPosition=new Vector3(-.0335f,-.095f,0);
        var eyes=Rect("Animated robot eyes",nameCanvas,240,100);c.Eyes=Group(eyes);var shapes=new List<RectTransform>();
        foreach(float x in new[]{-39f,39f}){var eye=Rect("Eye",eyes,20,32,x,0);var im=eye.gameObject.AddComponent<Image>();im.sprite=Sprite("Assets/Art/Sprites/Turrets/TurretRangeCircleSmooth.png");im.color=new Color(.5f,.87f,.82f);im.raycastTarget=false;shapes.Add(eye);}
        c.EyeShapes=shapes.ToArray();var scan=Rect("Screen boot scan",nameCanvas,200,2);scan.gameObject.AddComponent<Image>().color=new Color(.5f,.8f,.74f,.35f);c.ScreenScan=scan;
        var thruster=Effect(c.DroneRig,"Gentle drone stabilizer",0,11f,new Color(.3f,.69f,.84f),6,.17f,.025f,.28f);thruster.System.transform.localPosition=new Vector3(0,-.67f,0);c.Effects=new[]{thruster};
        c.Sounds=new[]{Sound(c.transform,"Drone contact response","TurretOn",1.82f,2.25f,.025f,1.65f),Sound(c.transform,"Drone display relay","TurretOff",5.7f,6.2f,.025f,1.5f)};
    }
}
