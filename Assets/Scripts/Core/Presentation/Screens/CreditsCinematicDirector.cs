using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    [DefaultExecutionOrder(-500)]
    public sealed class CreditsCinematicDirector : MonoBehaviour
    {
        public Camera Camera;
        public CreditsCinematicChapter[] Chapters;
        public CanvasGroup Black, Logo, FinalCredits;
        public RectTransform LogoContent;
        public AudioSource FlightAudio;
        public AudioSource Music;
        [Range(0, 1)] public float MusicVolume = .55f;
        private float _musicGain = 1;
        public RawImage Display;
        [Range(360, 1440)] public int RenderHeight = 1080;
        public RenderTexture RenderTarget { get; private set; }
        [Min(.1f)] public float ChapterFade = .7f;
        [Min(1)] public float LogoSeconds = 5;
        public bool AutoPlay = true;
        public event Action ExitRequested;
        public bool IsPlaying { get; private set; }
        public float TimePosition { get; private set; }
        public int ChapterIndex { get; private set; } = -1;
        public float ChaptersDuration { get { float t = 0; foreach (var c in Chapters) t += c.Duration; return t; } }
        public float FinalCreditsStart => ChaptersDuration + LogoSeconds;
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || Camera == null || Display == null) return;
            int height = Mathf.Clamp(RenderHeight, 360, 1440);
            RenderTarget = new RenderTexture(Mathf.RoundToInt(height * 16f/9), height, 24, RenderTextureFormat.ARGB32);
            RenderTarget.name = "Credits cinematic viewport"; RenderTarget.Create();
            Camera.targetTexture = RenderTarget; Camera.aspect = 16f/9;
            Display.texture = RenderTarget; Display.enabled = true;
        }
        private void Start() { if (AutoPlay) Play(); else EvaluateAt(0, true); }
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
            TimePosition = Mathf.Max(0, seconds); float offset = 0; int active = -1;
            if (Music != null)
            {
                if (seek) Music.Stop();
                SetMusicGain(_musicGain);
            }
            for (int i = 0; i < Chapters.Length; i++)
            {
                bool show = TimePosition >= offset && TimePosition < offset + Chapters[i].Duration;
                Chapters[i].gameObject.SetActive(show);
                if (show)
                {
                    active = i; float local = TimePosition - offset;
                    Chapters[i].Evaluate(local, Camera, seek, IsPlaying && !seek);
                    Camera.orthographicSize *= Mathf.Max(1, (16f/9) / Camera.aspect);
                    Black.alpha = Mathf.Max(1 - Ease(local / ChapterFade), Ease((local - Chapters[i].Duration + ChapterFade) / ChapterFade));
                    Black.alpha = Mathf.Max(Black.alpha, Chapters[i].InternalFade);
                }
                offset += Chapters[i].Duration;
            }
            ChapterIndex = active;
            float end = TimePosition - offset;
            Logo.alpha = end >= 0 && end < LogoSeconds ? Ease(end / .9f) * (1 - Ease((end - LogoSeconds + .9f) / .9f)) : 0;
            if (LogoContent != null) LogoContent.localScale = Vector3.one * Mathf.Lerp(.96f, 1.02f, Ease(end / LogoSeconds));
            FinalCredits.alpha = Ease((end - LogoSeconds) / 1.1f);
            if (active < 0) Black.alpha = 1;
            if (FlightAudio != null)
            {
                bool fly = active == 1 && IsPlaying && !seek;
                if (fly && !FlightAudio.isPlaying) FlightAudio.Play();
                if (!fly && FlightAudio.isPlaying) FlightAudio.Stop();
                FlightAudio.volume = fly ? .16f * (1 - Black.alpha) : 0;
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
