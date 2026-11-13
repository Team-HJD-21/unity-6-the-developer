// 기존 Main 시각 요소의 시작/종료 버튼을 Core 게임 흐름에 연결합니다.
// Match·저장·Scene 상태를 소유하지 않으며 화면 해제 때 자신의 구독만 제거합니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    public sealed class StartMenuPresenter : MonoBehaviour
    {
        [SerializeField] private Button _start;
        [SerializeField] private Button _exit;
        [SerializeField] private Button _settings;
        private GameFlowController _flow;
        private ScreenManager _screens;
        public bool IsConfigured => _start != null && _exit != null;

        /// <summary>Core 시작 Scene 제작 도구가 기존 버튼 참조를 저장합니다. Runtime에서 UI를 새로 디자인하지 않습니다.</summary>
        public void Configure(Button start, Button exit, Button settings = null)
        {
            if (_flow != null) throw new InvalidOperationException("Configure the view before binding.");
            _start = start != null ? start : throw new ArgumentNullException(nameof(start));
            _exit = exit != null ? exit : throw new ArgumentNullException(nameof(exit));
            _settings = settings;
        }
        /// <summary>시작 Scene 조립부가 App 소유 흐름을 주입합니다. 이 Presenter는 해당 흐름을 Dispose하지 않습니다.</summary>
        public void Bind(GameFlowController flow, ScreenManager screens = null)
        {
            if (!IsConfigured) throw new InvalidOperationException("Assign the existing Start/Exit buttons.");
            Unbind(); _flow = flow ?? throw new ArgumentNullException(nameof(flow)); _screens = screens;
            _flow.Changed += Render; _start.onClick.AddListener(StartGame); _exit.onClick.AddListener(ExitGame); Render();
            if (_settings != null) _settings.onClick.AddListener(OpenSettings);
        }
        public void Unbind()
        {
            if (_flow != null) _flow.Changed -= Render;
            _flow = null;
            _screens = null;
            if (_start != null) _start.onClick.RemoveListener(StartGame);
            if (_exit != null) _exit.onClick.RemoveListener(ExitGame);
            if (_settings != null) _settings.onClick.RemoveListener(OpenSettings);
        }
        private void Render()
        {
            if (_start != null) _start.interactable = _flow != null && !_flow.IsTransitioning && _flow.Phase == GameFlowPhase.MainMenu;
            if (_exit != null) _exit.interactable = _flow != null && !_flow.IsTransitioning;
            if (_settings != null) _settings.interactable = _flow != null && !_flow.IsTransitioning && _screens != null;
        }
        private async void StartGame()
        {
            var flow = _flow;
            if (flow == null || flow.IsTransitioning) return;
            try { await flow.EnterSpaceshipAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        private void ExitGame() => UnityEngine.Application.Quit();
        private void OpenSettings() => _screens?.ShowSettings();
        private void OnDisable() => Unbind();
    }
}
