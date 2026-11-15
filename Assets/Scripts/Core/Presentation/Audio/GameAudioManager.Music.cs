using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    public sealed partial class GameAudioManager
    {
        public GameplayMusicPlayer GameplayMusic { get; private set; }
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
        private void TickMusic(double dspNow) => GameplayMusic?.Tick(dspNow);
        private void ShutdownMusic() => GameplayMusic?.Dispose();
    }
}
