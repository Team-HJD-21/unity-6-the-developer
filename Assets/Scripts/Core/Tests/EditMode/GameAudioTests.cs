using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameAudioTests
    {
        private readonly string[] _keys = { GameAudioSettings.MasterKey, GameAudioSettings.MusicKey, GameAudioSettings.EffectsKey, "CBC.UI.HudScale" };
        private bool[] _had;
        private float[] _old;
        private SettingsScreenPresenter _presenter;

        [TestCase(0, -80)] [TestCase(.5f, -6.0206f)] [TestCase(1, 0)]
        public void VolumeHasFiniteDecibelsAndZeroIsTheMuteFloor(float linear, float expected)
        {
            Assert.That(GameAudioSettings.Decibels(linear), Is.EqualTo(expected).Within(.001));
            var settings = new GameAudioSettings(false); settings.Set(float.NaN, -1, 5);
            Assert.That(settings.Values.Master, Is.EqualTo(1)); Assert.That(settings.Values.Music, Is.Zero);
            Assert.That(settings.Values.Effects, Is.EqualTo(1));
        }

        [Test]
        public void AuthoredMixerAndSourcesUseNativeAssetsAndSeparateBuses()
        {
            var config = Resources.Load<GameAudioConfiguration>("Core/GameAudioConfiguration");
            Assert.That(config, Is.Not.Null); Assert.That(config.Mixer, Is.Not.Null);
            Assert.That(config.MusicOutput.name, Is.EqualTo("Music")); Assert.That(config.CinematicOutput.name, Is.EqualTo("Cinematic"));
            Assert.That(config.VoicePrefab.playOnAwake, Is.False); Assert.That(config.VoicePrefab.GetComponent<AudioListener>(), Is.Null);
            Assert.That(config.PoolSize, Is.LessThanOrEqualTo(30));
            Assert.That(config.Cues.Select(c => c.Id).Distinct().Count(), Is.EqualTo(config.Cues.Length));
            foreach (var cue in config.Cues)
            {
                Assert.That(cue.Class.Output, Is.Not.Null); Assert.That(cue.Class.WorldSpace, Is.True);
                Assert.That(cue.Clips, Has.All.Not.Null); Assert.That(cue.Clips.Length, Is.GreaterThan(0));
            }
            foreach (string path in new[] { "Assets/Prefabs/UI/Common/MenuSoundtrack.prefab", "Assets/Prefabs/UI/Cinematics/CreditsCinematic.prefab" })
            {
                var sources = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<AudioSource>(true);
                foreach (var source in sources) Assert.That(source.outputAudioMixerGroup, Is.Not.Null, source.name);
            }
            var view = Resources.Load<AppScreenStyle>("Core/AppScreenStyle").SettingsPrefab;
            Assert.That(view.HasAudioControls, Is.True);
            foreach (var slider in new[] { view.MasterVolume, view.MusicVolume, view.EffectsVolume })
            { Assert.That(slider.minValue, Is.Zero); Assert.That(slider.maxValue, Is.EqualTo(100)); }
        }

        [UnityTest]
        public IEnumerator PoolBoundsStaleHandlesPlanarDistanceAndSceneLifetime()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            if (GameAudioManager.Current != null) GameAudioManager.Current.Shutdown();
            var listener = new GameObject("Audio test listener", typeof(AudioListener)); listener.transform.position = new Vector3(0, 0, -50);
            var profile = ScriptableObject.CreateInstance<SoundClassProfile>(); profile.Bus = GameAudioBus.UI;
            profile.WorldSpace = false; profile.MaximumVoices = 8; profile.MaximumPerEmitter = 1; profile.RetriggerSeconds = 0;
            var cue = ScriptableObject.CreateInstance<GameAudioCue>(); cue.Class = profile; cue.Loop = true; cue.MaximumInstances = 8;
            cue.Clips = new[] { AudioClip.Create("silent pool test", 44100, 1, 44100, false) };
            var config = ScriptableObject.CreateInstance<GameAudioConfiguration>(); config.PoolSize = 4; config.ReservedUIVoices = 1;
            var manager = new GameObject("Test audio").AddComponent<GameAudioManager>(); manager.Initialize(config, new GameAudioSettings(false));
            var owners = Enumerable.Range(0, 6).Select(i => new GameObject("Emitter " + i)).ToArray();
            try
            {
                Assert.That(manager.TryPlay(cue, owners[0].transform, Vector3.zero, out var first), Is.True);
                Assert.That(manager.TryPlay(cue, owners[0].transform, Vector3.zero, out _), Is.False, "A looping cue is not duplicated for the same emitter.");
                for (int i = 1; i < 4; i++) Assert.That(manager.TryPlay(cue, owners[i].transform, Vector3.zero, out _), Is.True);
                Assert.That(manager.TryPlay(cue, owners[4].transform, Vector3.zero, out _), Is.False, "The fixed pool must not grow beyond its capacity.");
                Assert.That(manager.ActiveVoices, Is.EqualTo(4));
                first.Stop(); yield return new WaitForSecondsRealtime(.08f);
                Assert.That(first.IsActive, Is.False);
                Assert.That(manager.TryPlay(cue, owners[4].transform, Vector3.zero, out var replacement), Is.True);
                first.Stop(); yield return new WaitForSecondsRealtime(.06f);
                Assert.That(replacement.IsActive, Is.True, "An old handle must not stop a reused source.");
                manager.Shutdown(); Object.Destroy(manager.gameObject);
                profile.Bus = GameAudioBus.Enemy; profile.WorldSpace = true; profile.MaximumDistance = 10;
                manager = new GameObject("Planar audio").AddComponent<GameAudioManager>(); manager.Initialize(config, new GameAudioSettings(false));
                Assert.That(manager.TryPlay(cue, owners[0].transform, new Vector3(100, 0, 0), out _), Is.False);
                owners[0].transform.position = new Vector3(4, 0, 0);
                Assert.That(manager.TryPlay(cue, owners[0].transform, owners[0].transform.position, out var spatial), Is.True);
                var source = manager.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null);
                Assert.That(source.spatialBlend, Is.EqualTo(1)); Assert.That(source.dopplerLevel, Is.Zero);
                Assert.That(source.transform.position.z, Is.EqualTo(-50), "Only sound positions share the listener plane; gameplay transforms remain unchanged.");
                var scene = SceneManager.CreateScene("Audio lifetime test"); SceneManager.MoveGameObjectToScene(owners[0], scene);
                spatial.Stop(); yield return new WaitForSecondsRealtime(.08f);
                Assert.That(manager.TryPlay(cue, owners[0].transform, owners[0].transform.position, out spatial), Is.True);
                yield return SceneManager.UnloadSceneAsync(scene);
                Assert.That(spatial.IsActive, Is.False);
                cue.Loop = false;
                Assert.That(manager.TryPlay(cue, owners[1].transform, Vector3.zero, out var oldShot), Is.True);
                Assert.That(manager.TryPlay(cue, owners[1].transform, Vector3.zero, out var newShot), Is.True);
                Assert.That(oldShot.IsActive, Is.False); Assert.That(newShot.IsActive, Is.True);
                Assert.That(manager.ActiveVoices, Is.EqualTo(1), "A rapid new shot replaces its old tail instead of growing the emitter's voice count.");
            }
            finally
            {
                manager.Shutdown(); Object.Destroy(manager.gameObject); Object.Destroy(listener);
                foreach (var owner in owners) if (owner != null) Object.Destroy(owner);
                Object.Destroy(cue.Clips[0]); Object.Destroy(cue); Object.Destroy(profile); Object.Destroy(config);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SettingsPreviewCancelApplyAndDefaultsKeepOneTransaction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _had = _keys.Select(PlayerPrefs.HasKey).ToArray(); _old = _keys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
            var canvas = Object.Instantiate(Resources.Load<AppScreenStyle>("Core/AppScreenStyle").CanvasPrefab);
            var settings = new GameAudioSettings(false); settings.Set(.8f, .7f, .6f);
            var display = new UiDisplaySettings(false);
            System.Action close = () => { _presenter.Dispose(); _presenter = null; };
            void Open()
            {
                _presenter = new SettingsScreenPresenter(canvas.Screens, Resources.Load<AppScreenStyle>("Core/AppScreenStyle").SettingsPrefab,
                    display, close, () => { }, settings); _presenter.SetFocused(true);
            }
            Open(); yield return null;
            var view = canvas.GetComponentInChildren<AppSettingsView>();
            view.MusicVolume.value = 0; view.EffectsVolume.value = 32;
            Assert.That(settings.Values.Music, Is.Zero); Assert.That(settings.Values.Effects, Is.EqualTo(.32f));
            view.Back.onClick.Invoke(); Assert.That(settings.Values.Music, Is.EqualTo(.7f));
            Assert.That(settings.Values.Effects, Is.EqualTo(.6f));
            yield return new WaitForSecondsRealtime(.25f); Open(); yield return null;
            view = canvas.GetComponentInChildren<AppSettingsView>(); view.MasterVolume.value = 45; view.Apply.onClick.Invoke();
            Assert.That(new GameAudioSettings().Values.Master, Is.EqualTo(.45f));
            yield return new WaitForSecondsRealtime(.25f); Open(); yield return null;
            view = canvas.GetComponentInChildren<AppSettingsView>(); view.Defaults.onClick.Invoke();
            Assert.That(settings.Values.Master, Is.EqualTo(1)); view.Back.onClick.Invoke();
            Assert.That(settings.Values.Master, Is.EqualTo(.45f)); Object.Destroy(canvas.gameObject);
            RestorePreferences(); yield return new ExitPlayMode();
        }
        private void RestorePreferences()
        {
            if (_had == null) return;
            for (int i = 0; i < _keys.Length; i++) { if (_had[i]) PlayerPrefs.SetFloat(_keys[i], _old[i]); else PlayerPrefs.DeleteKey(_keys[i]); }
            PlayerPrefs.Save(); _had = null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _presenter?.Dispose(); _presenter = null; RestorePreferences();
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
