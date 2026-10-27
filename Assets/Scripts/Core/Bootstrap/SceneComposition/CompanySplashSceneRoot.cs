// 시작 Scene에서 회사 로고 재생을 조립하고 기존 SceneFlow로 실제 게임 진입을 이어갑니다.
// 다른 Scene 직접 Play와 Match 재시작에서는 스플래시를 강제로 재생하지 않습니다.
using System;
using System.Collections;
using System.Threading;
using TeamHJD.Game.Application;
using TeamHJD.Game.Presentation;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class CompanySplashSceneRoot : MonoBehaviour, ISceneCompositionRoot
    {
        [SerializeField] private string _nextScene = "Assets/Scenes/Gameplay/Initial_Stage.unity";
        private Coroutine _startup;
        private bool _composed;

        public void Compose(IAppMatchHost host)
        {
            if (_composed) return;
            if (!(host is AppRoot app)) throw new ArgumentException("Splash requires the App scene flow.", nameof(host));
            _composed = true;
            _startup = StartCoroutine(EnterGame(app));
        }
        private IEnumerator EnterGame(AppRoot app)
        {
            var player = gameObject.AddComponent<CompanySplashPlayer>();
            yield return player.Play("Branding/TeamHJD_Splash.mp4");
            if (app == null) yield break;
            var transition = app.SceneTransitions.ReplaceAsync(_nextScene, CancellationToken.None);
            while (!transition.IsCompleted) yield return null;
            if (transition.IsFaulted) Debug.LogException(transition.Exception, this);
            _startup = null;
        }
        private void OnDisable()
        {
            if (_startup != null) StopCoroutine(_startup);
            _startup = null;
        }
    }
}
