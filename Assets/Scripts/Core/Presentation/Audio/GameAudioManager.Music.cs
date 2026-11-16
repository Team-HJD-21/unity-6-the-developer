// 앱의 고정 BGM 재생자와 Scene 전환용 Music bus gain을 관리합니다.
// 설정 음량·현재 곡의 재생 위치는 보존하고 실제 DSP 시각으로 전환합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    public sealed partial class GameAudioManager
    {
        public GameplayMusicPlayer GameplayMusic { get; private set; }
        private float _sceneMusicGain = 1;
        private int _sceneMusicRequest;
        public float SceneMusicGain => _sceneMusicGain;
        private void InitializeMusic()
        {
            if (Configuration.GameplayMusic != null)
                GameplayMusic = new GameplayMusicPlayer(Configuration.GameplayMusic, transform, Configuration.VoicePrefab, _random);
        }
        public void SetGameplayMusic(GameplayMusicContext context, string scope)
        {
            if (_shutdown || !isActiveAndEnabled) return;
            GameplayMusic?.SetContext(context, scope, AudioSettings.dspTime);
        }
        /// <summary>Main/Credits가 기존 Source를 시작하기 전에 Gameplay 음악만 즉시 비웁니다.</summary>
        public void StopGameplayMusic() => GameplayMusic?.StopImmediately();
        /// <summary>Scene 전환 소유자가 기존 곡을 내리거나 새 Scene의 음악을 올립니다. 설정 음량과 재생 위치는 유지합니다.</summary>
        public async Task FadeSceneMusicAsync(float gain, float seconds, CancellationToken cancellation)
        {
            if (!float.IsFinite(gain) || !float.IsFinite(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            cancellation.ThrowIfCancellationRequested();
            int request = ++_sceneMusicRequest;
            float target = Mathf.Clamp01(gain), from = _sceneMusicGain;
            double started = AudioSettings.dspTime;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation, destroyCancellationToken);
            do
            {
                lifetime.Token.ThrowIfCancellationRequested();
                if (request != _sceneMusicRequest) throw new OperationCanceledException("새 BGM 전환 요청으로 교체되었습니다.");
                float progress = seconds == 0 || Mathf.Approximately(from, target) ? 1 :
                    Mathf.Clamp01((float)((AudioSettings.dspTime - started) / seconds));
                _sceneMusicGain = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, progress));
                ApplyMusicBusGain();
                if (progress >= 1) return;
                await Task.Yield();
            } while (true);
        }

        private void ApplyMusicBusGain()
        {
            if (Configuration?.Mixer == null || _settings == null) return;
            float user = GameAudioSettings.Decibels(_settings.Values.Music);
            float fade = _sceneMusicGain <= 0 ? -80 : 20 * Mathf.Log10(_sceneMusicGain);
            Configuration.Mixer.SetFloat(Configuration.MusicParameter, Mathf.Max(-80, user + fade));
        }
        private void TickMusic(double dspNow) => GameplayMusic?.Tick(dspNow);
        private void ShutdownMusic() => GameplayMusic?.Dispose();
    }
}
