using System;
using System.Linq;
using System.Reflection;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Unity 내장 믹서/AudioSource와 데이터 기본값을 제작합니다. 기존 음원과 Legacy 믹서는 수정하지 않습니다.</summary>
public static class GameAudioSetup
{
    public const string ConfigPath = "Assets/Resources/Core/GameAudioConfiguration.asset";
    public const string MixerPath = "Assets/Audio/Mixers/GameAudio.mixer";
    private const string DataFolder = "Assets/Audio/Configuration";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("Tools/TeamHJD/The Developer/Audio/Configure native audio")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the dirty Scene first.");
        EnsureFolder(DataFolder); EnsureFolder("Assets/Prefabs/Audio");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) ?? CreateNativeMixer();
        var config = AssetDatabase.LoadAssetAtPath<GameAudioConfiguration>(ConfigPath);
        bool fresh = config == null;
        if (fresh) { config = ScriptableObject.CreateInstance<GameAudioConfiguration>(); AssetDatabase.CreateAsset(config, ConfigPath); }
        config.Mixer = mixer;
        config.MusicOutput = Group(mixer, "Music"); config.CinematicOutput = Group(mixer, "Cinematic");
        if (config.VoicePrefab == null) config.VoicePrefab = VoicePrefab();
        var player = Profile(mixer, GameAudioBus.Player, true, 48, 6, .035f, 24);
        var turret = Profile(mixer, GameAudioBus.Turret, true, 100, 8, .06f, 28);
        var enemy = Profile(mixer, GameAudioBus.Enemy, true, 128, 6, .1f, 22);
        Profile(mixer, GameAudioBus.UI, false, 8, 3, .04f, 1);
        Profile(mixer, GameAudioBus.Ambience, true, 180, 3, .2f, 30);
        Profile(mixer, GameAudioBus.Cinematic, false, 64, 8, .04f, 1);
        var cues = new[]
        {
            Cue(GameSound.PlayerShot, player, "PlayerBullet", .36f, .035f, .32f),
            Cue(GameSound.TurretShot, turret, "Fire", .25f, .03f, .4f),
            Cue(GameSound.MissileLaunch, turret, "MissileLaunch", .35f),
            Cue(GameSound.Explosion, turret, "MissileExplosion", .4f),
            Cue(GameSound.TurretOn, turret, "TurretOn", .35f),
            Cue(GameSound.TurretOff, turret, "TurretOff", .35f),
            Cue(GameSound.WolfAttack, enemy, "WolfBite", .3f, .38f, .95f)
        };
        if (fresh) config.Cues = cues;
        else config.Cues = config.Cues.Concat(cues.Where(c => !config.Cues.Any(old => old != null && old.Id == c.Id))).ToArray();
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        PatchSettings();
        PatchMusic("Assets/Prefabs/UI/Common/MenuSoundtrack.prefab", mixer);
        PatchMusic("Assets/Prefabs/UI/Cinematics/CreditsCinematic.prefab", mixer);
        const string wolf = "Assets/Prefabs/Enemies/Runtime/Assassin/BlackWolf.prefab";
        var root = PrefabUtility.LoadPrefabContents(wolf);
        try
        {
            var animation = root.GetComponentInChildren<EnemyAnimation>(true);
            if (animation != null && animation.AbilitySound == null)
            { animation.AbilitySound = cues.Last(); PrefabUtility.SaveAsPrefabAsset(root, wolf); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("NATIVE_GAME_AUDIO_CONFIGURED");
    }

    private static AudioMixer CreateNativeMixer()
    {
        // Editor-only adapter to Unity's own mixer asset factory. No custom audio engine or DSP.
        var type = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
        var mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { MixerPath });
        var master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
        object Add(string name, object parent)
        {
            var child = type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { name, true });
            type.GetMethod("AddChildToParent", Flags).Invoke(mixer, new[] { child, parent });
            return child;
        }
        var music = Add("Music", master); var effects = Add("Effects", master);
        foreach (string name in new[] { "UI", "Player", "Enemy", "Turret", "Ambience", "Cinematic" }) Add(name, effects);
        var parameterType = type.Assembly.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
        var exposed = Array.CreateInstance(parameterType, 3);
        var groups = new[] { master, music, effects }; var names = new[] { "MasterVolume", "MusicVolume", "EffectsVolume" };
        var snapshot = type.GetProperty("TargetSnapshot", Flags).GetValue(mixer);
        for (int i = 0; i < groups.Length; i++)
        {
            var groupType = groups[i].GetType(); object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("guid", Flags).SetValue(parameter, groupType.GetMethod("GetGUIDForVolume", Flags).Invoke(groups[i], null));
            parameterType.GetField("name", Flags).SetValue(parameter, names[i]); exposed.SetValue(parameter, i);
            groupType.GetMethod("SetValueForVolume", Flags).Invoke(groups[i], new[] { (object)mixer, snapshot, 0f });
        }
        type.GetProperty("exposedParameters", Flags).SetValue(mixer, exposed);
        mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
        EditorUtility.SetDirty(mixer); AssetDatabase.SaveAssetIfDirty(mixer);
        return mixer;
    }
    private static AudioMixerGroup Group(AudioMixer mixer, string name)
        => mixer.FindMatchingGroups(name).Single(group => group.name == name);
    private static SoundClassProfile Profile(AudioMixer mixer, GameAudioBus bus, bool world, int priority, int voices, float retrigger, float distance)
    {
        string path = DataFolder + "/" + bus + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<SoundClassProfile>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<SoundClassProfile>(); asset.name = bus.ToString();
        asset.Bus = bus; asset.Output = Group(mixer, bus.ToString()); asset.WorldSpace = world; asset.Priority = priority;
        asset.MaximumVoices = voices; asset.RetriggerSeconds = retrigger; asset.MaximumDistance = distance;
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static GameAudioCue Cue(GameSound id, SoundClassProfile profile, string clip, float volume, float start = 0, float duration = 0)
    {
        string path = DataFolder + "/" + id + ".asset";
        var cue = AssetDatabase.LoadAssetAtPath<GameAudioCue>(path);
        if (cue != null) return cue;
        cue = ScriptableObject.CreateInstance<GameAudioCue>(); cue.name = id.ToString(); cue.Id = id; cue.Class = profile; cue.Volume = volume;
        cue.StartSeconds = start; cue.DurationSeconds = duration;
        cue.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/" + clip + ".wav") };
        if (cue.Clips[0] == null) throw new InvalidOperationException("Missing source sound: " + clip);
        AssetDatabase.CreateAsset(cue, path); return cue;
    }
    private static AudioSource VoicePrefab()
    {
        const string path = "Assets/Prefabs/Audio/AudioVoice.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<AudioSource>(path); if (existing != null) return existing;
        var root = new GameObject("Audio Voice", typeof(AudioSource));
        try
        {
            var source = root.GetComponent<AudioSource>(); source.playOnAwake = false; source.dopplerLevel = 0;
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<AudioSource>();
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static void PatchMusic(string path, AudioMixer mixer)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                bool music = source.name.Contains("once") || source.name.Contains("loop");
                source.outputAudioMixerGroup = Group(mixer, music ? "Music" : "Cinematic");
                if (music) source.priority = 0;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    public static void ApplySettings(AppSettingsView view)
    {
        var panel = view.Panel; panel.sizeDelta = new Vector2(panel.sizeDelta.x, 720);
        view.Preview.anchoredPosition = new Vector2(view.Preview.anchoredPosition.x, 110);
        void At(RectTransform rect, float x, float y)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); }
        TMP_Text Text(string name, string text, float x, float y, float width)
        {
            var old = panel.Find(name);
            var label = old != null ? old.GetComponent<TMP_Text>() : Object.Instantiate(view.Title, panel);
            label.name = name; label.text = text; label.font = view.ScaleValue.font; label.fontSize = 22; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.rectTransform.sizeDelta = new Vector2(width, 38);
            At(label.rectTransform, x, y); return label;
        }
        // Korean labels reuse the established body font from the existing HUD scale label.
        var bodyFont = panel.Find("HUD Scale Label").GetComponent<TMP_Text>().font;
        var title = Text("Audio Section", "오디오", 42, 310, 750); title.font = bodyFont;
        void Row(string name, string caption, float y, out Slider slider, out TMP_Text value)
        {
            var old = panel.Find(name + " Slider");
            slider = old != null ? old.GetComponent<Slider>() : Object.Instantiate(view.HudScale, panel);
            slider.name = name + " Slider"; slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            At((RectTransform)slider.transform, 222, y + 2);
            var label = Text(name + " Label", caption, 42, y, 170); label.font = bodyFont;
            value = Text(name + " Value", "100%", 688, y, 100); value.alignment = TextAlignmentOptions.MidlineRight;
        }
        Row("Master Volume", "전체", 365, out view.MasterVolume, out view.MasterValue);
        Row("Music Volume", "음악", 435, out view.MusicVolume, out view.MusicValue);
        Row("Effects Volume", "효과음", 505, out view.EffectsVolume, out view.EffectsValue);
        At((RectTransform)view.Defaults.transform, 42, 638); At((RectTransform)view.Back.transform, 412, 638); At((RectTransform)view.Apply.transform, 610, 638);
        var chain = new Selectable[] { view.HudScale, view.MasterVolume, view.MusicVolume, view.EffectsVolume, view.Defaults, view.Back, view.Apply };
        for (int i = 0; i < chain.Length; i++)
        {
            var nav = chain[i].navigation; nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = chain[(i + chain.Length - 1) % chain.Length]; nav.selectOnDown = chain[(i + 1) % chain.Length]; chain[i].navigation = nav;
        }
        view.ShowAudio(new AudioVolumeState(1, 1, 1));
    }
    private static void PatchSettings()
    {
        const string path = "Assets/Prefabs/UI/Common/SettingsScreen.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { ApplySettings(root.GetComponent<AppSettingsView>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
