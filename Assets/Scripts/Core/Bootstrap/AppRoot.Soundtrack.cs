using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed partial class AppRoot
    {
        private CameraManager _musicCamera;
        private MatchCameraFollow _musicFollow;
        private Transform _musicActor;
        private PlayerMatchPresence _musicPresence;
        private bool _musicHubActor;

        /// <summary>기존 출력 카메라가 따르는 정확한 로컬 actor와 확정 환경만 읽습니다. 전술 marker는 Player로 사용하지 않습니다.</summary>
        private void RenderGameplaySoundtrack()
        {
            if (Audio == null || Audio.GameplayMusic == null) return;
            if (GameFlow != null && (GameFlow.Phase == GameFlowPhase.MainMenu || GameFlow.Phase == GameFlowPhase.Startup || GameFlow.Phase == GameFlowPhase.Faulted))
            { Audio.StopGameplayMusic(); return; }
            var camera = CameraManager.Current;
            if (_musicCamera != camera)
            { _musicCamera = camera; _musicFollow = camera != null ? camera.GetComponent<MatchCameraFollow>() : null; }
            Transform actor = _musicFollow != null ? _musicFollow.Target : null;
            if (_musicActor != actor)
            {
                _musicActor = actor; _musicPresence = actor != null ? actor.GetComponent<PlayerMatchPresence>() : null;
                _musicHubActor = false;
                if (actor != null && actor.gameObject.scene.IsValid() && actor.gameObject.scene.isLoaded)
                    foreach (var root in actor.gameObject.scene.GetRootGameObjects())
                    {
                        var hub = root.GetComponentInChildren<SpaceshipHubCompositionRoot>(true);
                        if (hub != null && hub.Player == actor.gameObject) { _musicHubActor = true; break; }
                    }
            }
            GameplayMusicContext context = GameplayMusicContext.None; string scope = "";
            if (actor != null && actor.gameObject.activeInHierarchy)
            {
                var time = _musicPresence?.TimeSource;
                var clock = time?.Snapshot;
                if (_musicPresence != null && !_musicPresence.GameplayCancellation.IsCancellationRequested &&
                    time != null && clock != null && !clock.MatchId.IsEmpty)
                {
                    scope = clock.MatchId.Value;
                    context = GameplayMusicContextQueries.ReadMatch(clock.MatchId, time.IsAvailable, _musicPresence.Location, CurrentEnvironment);
                }
                else if (_musicHubActor && (GameFlow == null || GameFlow.Phase == GameFlowPhase.Spaceship))
                { context = GameplayMusicContext.Spaceship; scope = "hub/" + actor.gameObject.scene.handle; }
            }
            Audio.SetGameplayMusic(context, scope);
        }
    }
}
