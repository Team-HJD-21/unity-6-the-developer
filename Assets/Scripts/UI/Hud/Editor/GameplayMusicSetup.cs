using System;
using System.Linq;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>사용자 OST의 입고와 데이터 참조만 작성합니다. Main/Credits와 Settings 프리팹은 다시 만들지 않습니다.</summary>
public static class GameplayMusicSetup
{
    public const string Folder = "Assets/Audio/Music/OriginalSoundtrack";
    public const string LibraryPath = "Assets/Audio/Configuration/GameplayMusic.asset";
    private static readonly string[] Day = { "Day1ForwardMotion", "Day2SteelFrotier", "Day3OrbitalExchange", "Day4FrontierRelay" };
    private static readonly string[] Night = { "Night1AfterSundown", "Night2IronVigil", "Night3GhostCircuit", "Night4DistanceLights" };
    private const string Ship = "SpaceshipAHomeBetweenStars";

    [MenuItem("Tools/TeamHJD/The Developer/Audio/Configure gameplay soundtrack")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the dirty Scene first.");
        var names = Day.Concat(Night).Append(Ship).ToArray();
        foreach (string name in names)
        {
            string path = Folder + "/" + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Import the provided soundtrack first: " + path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming; settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .9f; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            bool changed = importer.defaultSampleSettings.loadType != settings.loadType ||
                importer.defaultSampleSettings.compressionFormat != settings.compressionFormat ||
                !Mathf.Approximately(importer.defaultSampleSettings.quality, settings.quality) ||
                importer.defaultSampleSettings.sampleRateSetting != settings.sampleRateSetting ||
                importer.defaultSampleSettings.preloadAudioData || !importer.loadInBackground || importer.forceToMono;
            settings.preloadAudioData = false; importer.defaultSampleSettings = settings;
            importer.loadInBackground = true; importer.forceToMono = false;
            if (changed) importer.SaveAndReimport();
        }
        var library = AssetDatabase.LoadAssetAtPath<GameplayMusicLibrary>(LibraryPath);
        bool fresh = library == null;
        if (fresh) { library = ScriptableObject.CreateInstance<GameplayMusicLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
        // 작성된 배열과 수동 믹스 조정은 보존합니다. 처음 생성하거나 비어 있는 연결만 입고합니다.
        if (fresh || library.Day.Length == 0) library.Day = Day.Select(Clip).ToArray();
        if (fresh || library.Night.Length == 0) library.Night = Night.Select(Clip).ToArray();
        if (library.Spaceship == null) library.Spaceship = Clip(Ship);
        if (library.Day.Any(c => c == null) || library.Night.Any(c => c == null) || library.Spaceship == null)
            throw new InvalidOperationException("The soundtrack has missing clip references.");
        var config = AssetDatabase.LoadAssetAtPath<GameAudioConfiguration>(GameAudioSetup.ConfigPath);
        if (config == null) throw new InvalidOperationException("Configure native audio before its gameplay soundtrack.");
        config.GameplayMusic = library;
        EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        Debug.Log("GAMEPLAY_SOUNDTRACK_AUTHORED");
    }
    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + ".wav");
}
