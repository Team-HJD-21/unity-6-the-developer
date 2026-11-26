using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    [DefaultExecutionOrder(-500)]
    public sealed class CreditsCinematicDirector : MonoBehaviour
    {
        public Camera Camera;
        public CreditsCinematicChapter[] Chapters;
        public CanvasGroup Black, FinalCredits;
        public AudioSource FlightAudio;
        public AudioSource Music;
        [Range(0, 1)] public float MusicVolume = .55f;
        private float _musicGain = 1;
        public RawImage Display;
        [Range(360, 1440)] public int RenderHeight = 1080;
        public RenderTexture RenderTarget { get; private set; }
        public bool AutoPlay = true;
        [SerializeField] private PlayableDirector _timeline;
        private TimelineClip[] _chapterClips;
        private TimelineClip _creditsClip;
        public event Action ExitRequested;
        public bool IsPlaying { get; private set; }
        public float TimePosition { get; private set; }
        public int ChapterIndex { get; private set; } = -1;
        public float FinalCreditsStart
        {
            get
            {
                BindTimeline();
                return (float)_creditsClip.start;
            }
        }
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || Camera == null || Display == null) return;
            int height = Mathf.Clamp(RenderHeight, 360, 1440);
            RenderTarget = new RenderTexture(Mathf.RoundToInt(height * 16f/9), height, 24, RenderTextureFormat.ARGB32);
            RenderTarget.name = "Credits cinematic viewport"; RenderTarget.Create();
            Camera.targetTexture = RenderTarget; Camera.aspect = 16f/9;
            Display.texture = RenderTarget; Display.enabled = true;
        }
        private void Start()
        {
            if (IsPlaying) return; // 메인에서 Additive 로드 후 이미 시작한 재생을 처음으로 되돌리지 않습니다.
            if (AutoPlay) Play();
            else EvaluateAt(0);
        }
        public void Play()
        {
            GameAudioManager.RouteMusic(Music); GameAudioManager.RouteCinematic(FlightAudio);
            foreach (var chapter in Chapters)
                if (chapter != null) foreach (var cue in chapter.Sounds) GameAudioManager.RouteCinematic(cue.Source);
            TimePosition = 0; IsPlaying = true; EvaluateAt(0, true);
            if (Music != null) { Music.volume = 0; Music.Play(); }
        }
        public void SetMusicGain(float gain)
        {
            _musicGain = Mathf.Clamp01(gain);
            if (Music != null) Music.volume = MusicVolume * _musicGain * Ease(TimePosition / .4f);
        }
        public void Pause()
        {
            IsPlaying = false;
            if (_timeline != null) _timeline.Stop();
            if (Music != null) Music.Stop();
            if (FlightAudio != null) FlightAudio.Stop();
            foreach (var chapter in Chapters) if (chapter != null) chapter.StopAudio();
        }
        private void Update()
        {
            if (UIEscapeInput.TryConsume()) { ExitRequested?.Invoke(); return; }
            if (TimePosition >= FinalCreditsStart + 1 && Mouse.current?.leftButton.wasPressedThisFrame == true)
            { ExitRequested?.Invoke(); return; }
            if (IsPlaying) EvaluateAt(TimePosition + Mathf.Min(Time.unscaledDeltaTime, .1f), false);
        }
        /// <summary>Scene/Prefab의 같은 트랙을 실제 재생과 프레임 검수에서 함께 사용합니다.</summary>
        public void EvaluateAt(float seconds, bool seek = true)
        {
            TimePosition = Mathf.Max(0, seconds);
            if (Music != null)
            {
                if (seek) Music.Stop();
                SetMusicGain(_musicGain);
            }
            ChapterIndex = EvaluateTimeline(seek);
            if (FlightAudio != null)
            {
                bool fly = ChapterIndex >= 0 && Chapters[ChapterIndex].Kind == CreditsChapterKind.Flight && IsPlaying && !seek;
                if (fly && !FlightAudio.isPlaying) FlightAudio.Play();
                if (!fly && FlightAudio.isPlaying) FlightAudio.Stop();
                FlightAudio.volume = fly ? .16f * (1 - Black.alpha) : 0;
            }
        }

        private int EvaluateTimeline(bool seek)
        {
            BindTimeline();
            // 한 시각으로 카메라·암전·로고·명단과 챕터 효과를 평가해 Seek에서도 같은 장면을 보여 줍니다.
            _timeline.time = Math.Min(TimePosition, _timeline.duration - .00001);
            _timeline.Evaluate();
            for (int i = 0; i < _chapterClips.Length; i++)
            {
                var clip = _chapterClips[i];
                if (TimePosition < clip.start || TimePosition >= clip.end) continue;
                Chapters[i].Evaluate((float)(TimePosition - clip.start), (float)clip.duration, seek, IsPlaying && !seek);
                return i;
            }
            return -1;
        }

        private void BindTimeline()
        {
            if (_chapterClips != null) return;
            if (_timeline == null || !(_timeline.playableAsset is TimelineAsset asset) ||
                !double.IsFinite(asset.duration) || asset.duration <= 0)
                throw new InvalidOperationException("Credits requires a finite Timeline asset.");

            // Activation 트랙의 실제 바인딩으로 컷을 찾으므로 Timeline 트랙 이름은 자유롭게 편집할 수 있습니다.
            var activationTracks = asset.GetOutputTracks().OfType<ActivationTrack>().ToArray();
            var chapters = Chapters.Select(chapter => FindActivationClip(chapter.gameObject)).ToArray();
            _creditsClip = FindActivationClip(FinalCredits.gameObject);
            var ordered = chapters.OrderBy(clip => clip.start).Append(_creditsClip).ToArray();
            for (int i = 1; i < ordered.Length; i++)
                if (ordered[i].start + .00001 < ordered[i - 1].end)
                    throw new InvalidOperationException("Credits chapter and final credits activation clips cannot overlap.");
            _timeline.timeUpdateMode = DirectorUpdateMode.Manual;
            _chapterClips = chapters;

            TimelineClip FindActivationClip(GameObject target)
            {
                var clips = activationTracks.Where(track => !track.mutedInHierarchy && _timeline.GetGenericBinding(track) == target)
                    .SelectMany(track => track.GetClips()).ToArray();
                if (clips.Length != 1)
                    throw new InvalidOperationException($"Credits Timeline requires one activation clip for '{target.name}'.");
                return clips[0];
            }
        }

        public static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        private void OnDisable()
        {
            Pause();
            if (Camera != null) Camera.targetTexture = null;
            if (Display != null) Display.texture = null;
            if (RenderTarget != null) { RenderTarget.Release(); Destroy(RenderTarget); RenderTarget = null; }
        }
    }
}
