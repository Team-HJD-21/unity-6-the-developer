using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameplayMusicTests
    {
        private GameAudioManager _manager;
        private GameAudioConfiguration _config;
        private GameplayMusicLibrary _library;
        private AudioClip[] _clips;
        private GameAudioSettings _settings;
        private GameObject _listener;
        private GameplayMusicPlayer _manualMusic;
        private float? _timeScaleBefore;

        private sealed class ControlledMusicData : IGameplayMusicData
        {
            private readonly System.Collections.Generic.HashSet<AudioClip> _blocked = new();
            public readonly System.Collections.Generic.Dictionary<AudioClip,int> Requests = new();
            public void Block(System.Collections.Generic.IEnumerable<AudioClip> clips) { foreach(var clip in clips) _blocked.Add(clip); }
            public void Release(System.Collections.Generic.IEnumerable<AudioClip> clips) { foreach(var clip in clips) _blocked.Remove(clip); }
            public AudioDataLoadState GetState(AudioClip clip) => !_blocked.Contains(clip) ? clip.loadState :
                Requests.ContainsKey(clip) ? AudioDataLoadState.Loading : AudioDataLoadState.Unloaded;
            public void Request(AudioClip clip) { Requests.TryGetValue(clip,out int count); Requests[clip]=count+1; }
        }

        [Test]
        public void MatchContextRequiresMatchingClockEnvironmentAndNeverDefaultsToDay()
        {
            var match=new MatchId("music-match");
            MatchEnvironmentSnapshot State(string phase) => new MatchEnvironmentSnapshot(match,1,0,default,new DefinitionId(phase));
            Assert.That(GameplayMusicContextQueries.ReadMatch(default,true,MatchPlayerLocation.Field,State("day")),Is.EqualTo(GameplayMusicContext.None));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,true,MatchPlayerLocation.Field,null),Is.EqualTo(GameplayMusicContext.None));
            Assert.That(GameplayMusicContextQueries.ReadMatch(new MatchId("other"),true,MatchPlayerLocation.Field,State("day")),Is.EqualTo(GameplayMusicContext.None));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,false,MatchPlayerLocation.Field,State("day")),Is.EqualTo(GameplayMusicContext.None));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,true,MatchPlayerLocation.Field,State("unknown")),Is.EqualTo(GameplayMusicContext.None));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,true,MatchPlayerLocation.Field,State("day")),Is.EqualTo(GameplayMusicContext.Day));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,true,MatchPlayerLocation.Field,State("night")),Is.EqualTo(GameplayMusicContext.Night));
            Assert.That(GameplayMusicContextQueries.ReadMatch(match,false,MatchPlayerLocation.Spaceship,null),Is.EqualTo(GameplayMusicContext.Spaceship));
        }

        [Test]
        public void ShuffleUsesEveryUniqueCandidateBeforeRepeatAndAvoidsBagBoundaryRepeat()
        {
            var values = new[] { "day-1", "day-2", "day-3", "day-4", null, "day-1" };
            var bag = new MusicShuffleBag<string>(values, new System.Random(42));
            Assert.That(bag.Count, Is.EqualTo(4)); string previous = null;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                string[] chosen = Enumerable.Range(0, 4).Select(_ => bag.Next()).ToArray();
                Assert.That(chosen.Distinct().Count(), Is.EqualTo(4));
                Assert.That(chosen[0], Is.Not.EqualTo(previous)); previous = chosen[3];
            }
        }
        [Test]
        public void DayAndNightDrawOnlyTheirOwnPoolsAndEmptyPoolDoesNotFallback()
        {
            var random = new System.Random(12);
            var day = new MusicShuffleBag<string>(new[] { "day-1", "day-2" }, random);
            var night = new MusicShuffleBag<string>(new[] { "night-1", "night-2" }, random);
            for (int i = 0; i < 20; i++)
            { Assert.That(day.Next(), Does.StartWith("day-")); Assert.That(night.Next(), Does.StartWith("night-")); }
            Assert.That(new MusicShuffleBag<string>(null, random).Next(), Is.Null);
            var single = new MusicShuffleBag<string>(new[] { "single", "single" }, random);
            Assert.That(single.Count, Is.EqualTo(1)); Assert.That(single.Next(), Is.EqualTo(single.Next()));
        }
        [Test]
        public void AuthoredNineTracksUseSeparatePoolsStreamingAndNoStartupPreload()
        {
            var config = Resources.Load<GameAudioConfiguration>("Core/GameAudioConfiguration");
            var library = config.GameplayMusic; Assert.That(library, Is.Not.Null);
            Assert.That(library.Day.Length, Is.EqualTo(4)); Assert.That(library.Night.Length, Is.EqualTo(4));
            var all = library.Day.Concat(library.Night).Append(library.Spaceship).ToArray();
            Assert.That(all, Has.All.Not.Null); Assert.That(all.Distinct().Count(), Is.EqualTo(9));
            foreach (var clip in all)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), clip.name);
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.False, clip.name);
                Assert.That(importer.loadInBackground, Is.True, clip.name);
                Assert.That(importer.forceToMono, Is.False, clip.name);
                Assert.That(clip.frequency, Is.EqualTo(48000)); Assert.That(clip.channels, Is.EqualTo(2));
            }
            foreach(var clip in library.Day) Assert.That(clip.name,Does.StartWith("Day"));
            foreach(var clip in library.Night) Assert.That(clip.name,Does.StartWith("Night"));
            Assert.That(library.TransitionSeconds, Is.InRange(.6f,.9f));
        }
        [UnityTest]
        public IEnumerator DelayedDataAndDayNightShipDayRequestsKeepOriginalFadeDeadlineAndLatestPool()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); CreateFixture(4,4.2f,false,true);
            _library.TransitionSeconds=.8f; _library.StartDelaySeconds=.02f;
            var data=new ControlledMusicData(); data.Block(_library.Night); data.Block(new[]{_library.Spaceship});
            _manualMusic=new GameplayMusicPlayer(_library,_manager.transform,null,new System.Random(73),data);
            _manualMusic.SetContext(GameplayMusicContext.Day,"delayed-match",AudioSettings.dspTime);
            yield return StepMusic(1);
            Time.timeScale=0; // 개인 메뉴는 월드 시간을 멈추지 않으며 이보다 강한 조건에서도 음악 DSP는 독립입니다.
            _manualMusic.SetContext(GameplayMusicContext.Night,"delayed-match",AudioSettings.dspTime);
            Assert.That(_manualMusic.PendingClip,Is.Not.Null); Assert.That(_manualMusic.CurrentClip,Is.Null);
            yield return StepMusic(.42f);
            Assert.That(_manualMusic.Sources.Any(s=>s.clip!=null&&_library.Day.Contains(s.clip)),Is.True);
            data.Release(_library.Night);
            yield return StepMusic(.45f);
            Assert.That(_manualMusic.Sources.All(s=>s.clip==null||!_library.Day.Contains(s.clip)),Is.True,
                "늦은 incoming 로딩 완료가 old Day의 원래0.8초 fade를 연장하지 않습니다.");
            Assert.That(_library.Night,Does.Contain(_manualMusic.CurrentClip));
            Assert.That(_manualMusic.Sources.Any(s=>s.clip!=null&&s.timeSamples>0),Is.True,"게임 배속0에서도 실제 AudioSource가 진행합니다.");
            yield return StepMusic(.5f);
            data.Block(_library.Day);
            _manualMusic.SetContext(GameplayMusicContext.Day,"delayed-match",AudioSettings.dspTime); yield return StepMusic(.03f);
            _manualMusic.SetContext(GameplayMusicContext.Spaceship,"delayed-match",AudioSettings.dspTime); yield return StepMusic(.03f);
            _manualMusic.SetContext(GameplayMusicContext.Day,"delayed-match",AudioSettings.dspTime);
            data.Release(new[]{_library.Spaceship}); yield return StepMusic(.05f);
            Assert.That(_manualMusic.Sources.All(s=>s.clip!=_library.Spaceship),Is.True,"취소된 Spaceship 로드 완료가 재생을 시작하지 않습니다.");
            data.Release(_library.Day); yield return StepMusic(1);
            Assert.That(_manualMusic.Context,Is.EqualTo(GameplayMusicContext.Day));
            Assert.That(_library.Day,Does.Contain(_manualMusic.CurrentClip));
            Assert.That(_manualMusic.PendingClip,Is.Null);
            foreach(var source in _manualMusic.Sources.Where(s=>s.clip!=null)) Assert.That(_library.Day,Does.Contain(source.clip));
            Assert.That(data.Requests.Where(p=>_library.Night.Contains(p.Key)).Sum(p=>p.Value),Is.EqualTo(1),"Loading 중 매 프레임 로드를 다시 요청하지 않습니다.");
            CleanupFixture(); yield return new ExitPlayMode();
        }

        private IEnumerator StepMusic(float seconds)
        {
            double end=Time.realtimeSinceStartupAsDouble+seconds;
            do { _manualMusic.Tick(AudioSettings.dspTime); yield return null; } while(Time.realtimeSinceStartupAsDouble<end);
            _manualMusic.Tick(AudioSettings.dspTime);
        }

        [UnityTest]
        public IEnumerator ShortTracksAdvanceByDspAndRapidPhaseChangesLeaveOnlyLatestPool()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); CreateFixture(.35f, .4f);
            var music = _manager.GameplayMusic;
            _manager.SetGameplayMusic(GameplayMusicContext.Day, "match-a");
            AudioClip first = music.CurrentClip;
            yield return new WaitForSecondsRealtime(.28f);
            Assert.That(music.QueuedClip, Is.Not.Null, "다음 같은 시간대 곡을 첫 곡이 끝나기 전에 예약합니다.");
            double end = music.CurrentEndsAt;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(AudioSettings.dspTime, Is.GreaterThan(end));
            Assert.That(music.CurrentClip, Is.Not.EqualTo(first)); Assert.That(_library.Day, Does.Contain(music.CurrentClip));
            Assert.That(music.Sources.Count, Is.EqualTo(2));
            _manager.SetGameplayMusic(GameplayMusicContext.Night, "match-a");
            yield return new WaitForSecondsRealtime(.025f);
            _manager.SetGameplayMusic(GameplayMusicContext.Day, "match-a");
            yield return new WaitForSecondsRealtime(.13f);
            Assert.That(music.Context, Is.EqualTo(GameplayMusicContext.Day));
            foreach (var source in music.Sources.Where(s=>s.clip!=null)) Assert.That(_library.Day, Does.Contain(source.clip));
            yield return new WaitForSecondsRealtime(.45f);
            foreach (var source in music.Sources.Where(s=>s.clip!=null)) Assert.That(_library.Day, Does.Contain(source.clip), "취소된 Night 예약이 뒤늦게 시작하지 않습니다.");
            _manager.SetGameplayMusic(GameplayMusicContext.Spaceship, "match-a");
            yield return new WaitForSecondsRealtime(.13f);
            Assert.That(music.CurrentClip, Is.SameAs(_library.Spaceship));
            Assert.That(music.Sources.Single(s=>s.clip!=null).loop, Is.True);
            _manager.SetGameplayMusic(GameplayMusicContext.None, "");
            yield return new WaitForSecondsRealtime(.13f);
            Assert.That(music.Sources.All(s=>s.clip==null&&!s.isPlaying), Is.True);
            CleanupFixture(); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator MissingNightPoolFadesOutDayInsteadOfPlayingAnIncorrectCategory()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); CreateFixture(2,2.2f,true);
            var music=_manager.GameplayMusic; _manager.SetGameplayMusic(GameplayMusicContext.Day,"match-empty-night");
            yield return new WaitForSecondsRealtime(.15f); Assert.That(music.CurrentClip,Is.Not.Null);
            _manager.SetGameplayMusic(GameplayMusicContext.Night,"match-empty-night");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(music.Context,Is.EqualTo(GameplayMusicContext.Night));
            Assert.That(music.CurrentClip,Is.Null); Assert.That(music.Sources.All(s=>s.clip==null),Is.True);
            CleanupFixture(); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SameContextAndVolumePreviewDoNotRestartAndShutdownCancelsFutureStarts()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); CreateFixture(2, 2.2f);
            var music = _manager.GameplayMusic;
            _manager.SetGameplayMusic(GameplayMusicContext.Day, "");
            Assert.That(music.CurrentClip, Is.Null, "원본 Match/Hub scope 없이 음악을 시작하지 않습니다.");
            _manager.SetGameplayMusic(GameplayMusicContext.Day, "match-b");
            yield return new WaitForSecondsRealtime(.18f);
            var current = music.CurrentClip; var source = music.Sources.Single(s=>s.clip==current);
            int sample = source.timeSamples;
            _settings.Set(1,0,1); _manager.SetGameplayMusic(GameplayMusicContext.Day,"match-b");
            Assert.That(music.Sources.All(s=>s.mute), Is.True);
            yield return new WaitForSecondsRealtime(.06f);
            _settings.Set(1,.4f,1);
            Assert.That(music.CurrentClip, Is.SameAs(current)); Assert.That(source.timeSamples, Is.GreaterThan(sample));
            Assert.That(music.Sources.All(s=>!s.mute), Is.True);
            _manager.StopGameplayMusic(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(music.Sources.All(s=>s.clip==null&&!s.isPlaying), Is.True);
            _manager.SetGameplayMusic(GameplayMusicContext.Night,"match-b"); _manager.Shutdown();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(music.Sources.All(s=>s.clip==null&&!s.isPlaying), Is.True);
            CleanupFixture(); yield return new ExitPlayMode();
        }
        private void CreateFixture(float first, float second, bool missingNight=false, bool manualMusic=false)
        {
            _timeScaleBefore=Time.timeScale;
            if (GameAudioManager.Current != null) GameAudioManager.Current.Shutdown();
            _listener = new GameObject("Music test listener", typeof(AudioListener));
            AudioClip Clip(string name,float length) => AudioClip.Create(name,Mathf.CeilToInt(48000*length),2,48000,false);
            _clips = new[] {Clip("Day-a",first),Clip("Day-b",second),Clip("Night-a",first),Clip("Night-b",second),Clip("Spaceship",first)};
            _library = ScriptableObject.CreateInstance<GameplayMusicLibrary>();
            _library.Day = _clips.Take(2).ToArray(); _library.Night = missingNight ? System.Array.Empty<AudioClip>() : _clips.Skip(2).Take(2).ToArray(); _library.Spaceship = _clips[4];
            _library.TransitionSeconds=.08f; _library.StartDelaySeconds=.04f; _library.ScheduleAheadSeconds=.2f;
            _config = ScriptableObject.CreateInstance<GameAudioConfiguration>(); _config.GameplayMusic = manualMusic ? null : _library;
            var authored = Resources.Load<GameAudioConfiguration>("Core/GameAudioConfiguration");
            _config.Mixer=authored.Mixer; _config.MusicOutput=authored.MusicOutput; _config.CinematicOutput=authored.CinematicOutput;
            _settings = new GameAudioSettings(false);
            _manager = new GameObject("Gameplay music test").AddComponent<GameAudioManager>(); _manager.Initialize(_config,_settings);
        }
        private void CleanupFixture()
        {
            _manualMusic?.Dispose(); _manualMusic=null;
            if(_timeScaleBefore.HasValue) { Time.timeScale=_timeScaleBefore.Value; _timeScaleBefore=null; }
            if(_manager!=null) { _manager.Shutdown(); Object.Destroy(_manager.gameObject); _manager=null; }
            if(_listener!=null) Object.Destroy(_listener);
            if(_clips!=null) foreach(var clip in _clips) if(clip!=null) Object.Destroy(clip);
            if(_library!=null) Object.Destroy(_library); if(_config!=null) Object.Destroy(_config);
            _clips=null; _library=null; _config=null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { CleanupFixture(); if(EditorApplication.isPlaying) yield return new ExitPlayMode(); }
    }
}
