using System;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>OST 원본을 보존하고 Main/크레딧의 편집 가능한 AudioSource만 연결합니다.</summary>
public static class FrontendSoundtrackSetup
{
    public const string MusicFolder = "Assets/Audio/Music/OriginalSoundtrack";
    public const string MenuPrefab = "Assets/Prefabs/UI/Common/MenuSoundtrack.prefab";

    [MenuItem("Tools/TeamHJD/The Developer/Soundtrack/Apply original soundtrack")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the open dirty Scene first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            AssetDatabase.Refresh();
            foreach (string name in new[]{"FirstContact","TheLastStand","AnotherJourney"})
            {
                string path=MusicFolder+"/"+name+".wav";
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;
                settings.quality=.9f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
            }
            EnsureMenuPrefab();
            var root=PrefabUtility.LoadPrefabContents(CreditsSceneSetup.Prefab);
            try
            {
                ConfigureCredits(root.GetComponent<CreditsCinematicDirector>());
                PrefabUtility.SaveAsPrefabAsset(root,CreditsSceneSetup.Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var startup=EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap/Startup.unity");
            ConfigureMenu(startup);EditorSceneManager.SaveScene(startup);
            Debug.Log("ORIGINAL_SOUNDTRACK_APPLIED");
        }
        finally { if(setup.Length>0&&setup.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    private static AudioClip Clip(string name)
        => AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder+"/"+name+".wav") ?? throw new InvalidOperationException("Missing OST: "+name);
    private static AudioSource Source(Transform parent,string name,AudioClip clip,bool loop)
    {
        var source=new GameObject(name).AddComponent<AudioSource>();source.transform.SetParent(parent,false);
        source.clip=clip;source.loop=loop;source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;
        source.volume=0;source.priority=32;return source;
    }
    private static void EnsureMenuPrefab()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefab)!=null)return;
        var root=new GameObject("Menu Soundtrack");
        try
        {
            var music=root.AddComponent<MenuSoundtrack>();
            music.Intro=Source(root.transform,"First Contact - once",Clip("FirstContact"),false);
            music.Ambient=Source(root.transform,"The Last Stand - loop",Clip("TheLastStand"),true);
            PrefabUtility.SaveAsPrefabAsset(root,MenuPrefab);
        }
        finally { Object.DestroyImmediate(root); }
    }
    public static void ConfigureMenu(Scene scene)
    {
        EnsureMenuPrefab();
        var root=scene.GetRootGameObjects().Single(r=>r.GetComponent<StartupSceneRoot>()!=null);
        var music=root.GetComponentInChildren<MenuSoundtrack>(true);
        if(music==null)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefab),scene);
            instance.transform.SetParent(root.transform,false);music=instance.GetComponent<MenuSoundtrack>();
        }
        root.GetComponent<StartupSceneRoot>().ConfigureMusic(music);EditorUtility.SetDirty(root.GetComponent<StartupSceneRoot>());
        var launcher=root.GetComponent<CreditsSceneLauncher>();
        if(launcher!=null){launcher.MenuMusic=music;EditorUtility.SetDirty(launcher);}
    }
    public static void ConfigureCredits(CreditsCinematicDirector director)
    {
        if(director.Music==null)director.Music=Source(director.transform,"Another Journey - once",Clip("AnotherJourney"),false);
        else { director.Music.clip=Clip("AnotherJourney");director.Music.loop=false;director.Music.playOnAwake=false; }
        EditorUtility.SetDirty(director);
    }
}
