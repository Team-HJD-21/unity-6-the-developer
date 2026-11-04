// Core 소유 시작 Scene에 App의 게임 경로와 기존 버튼 Presenter를 연결합니다.
// 다른 Scene을 강제로 시작 화면으로 바꾸지 않으므로 Initial_Stage 직접 검사는 유지됩니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class StartupSceneRoot : MonoBehaviour, ISceneCompositionRoot
    {
        [SerializeField] private StartMenuPresenter _view;
        [SerializeField] private string _spaceshipScene = "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity";
        public void Configure(StartMenuPresenter view) => _view = view != null ? view : throw new ArgumentNullException(nameof(view));
        public void Compose(IAppMatchHost appMatchHost)
        {
            if (!(appMatchHost is AppRoot app)) throw new InvalidOperationException("The startup Scene requires the App host.");
            if (_view == null || !_view.IsConfigured) throw new InvalidOperationException("Provide the authored startup view.");
            var flow = app.ConfigureGameFlow(new GameFlowRoutes(gameObject.scene.path, _spaceshipScene));
            if (flow.Phase == GameFlowPhase.Startup) flow.AdoptMainMenu();
            _view.Bind(flow);
        }
        private void OnDestroy() { if (_view != null) _view.Unbind(); }
    }
}
