// Match가 없어도 App의 시작·허브·Mission 진입 상태와 실패 원인을 보여줍니다.
// 실제 제작된 콘솔 UI가 연결되기 전 Core API를 명시적으로 확인하는 개발 도구입니다.
using System;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    internal static class GameDebugGameFlowPanel
    {
        private static AppRoot _app;
        private static string _mission = "Assets/Scenes/Gameplay/Initial_Stage.unity";
        public static void Draw()
        {
            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField("APP · GAME FLOW", EditorStyles.boldLabel);
            if (!UnityEngine.Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Core Startup Scene을 열고 Play하면 시작→우주선 흐름을 확인할 수 있습니다.", MessageType.Info);
                return;
            }
            if (_app == null) _app = UnityEngine.Object.FindAnyObjectByType<AppRoot>();
            var flow = _app == null ? null : _app.GameFlow;
            if (flow == null)
            {
                EditorGUILayout.HelpBox("현재 App의 게임 경로가 미구성입니다. Startup Scene으로 시작하세요. Initial_Stage 직접 실행은 별도 검사 경로입니다.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("위치 / 전환", flow.Phase + (flow.IsTransitioning ? " · TRANSITIONING" : ""));
            EditorGUILayout.SelectableLabel(flow.CurrentSceneKey ?? "—", EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            _mission = EditorGUILayout.TextField("Mission Scene", _mission);
            using (new EditorGUI.DisabledScope(flow.IsTransitioning))
            {
                if (flow.Phase == GameFlowPhase.MainMenu && GUILayout.Button("우주선 입장")) Run(() => flow.EnterSpaceshipAsync());
                if (flow.Phase == GameFlowPhase.Spaceship && GUILayout.Button("선택 Mission 출격")) Run(() => flow.LaunchMissionAsync(_mission));
                if ((flow.Phase == GameFlowPhase.Mission || flow.Phase == GameFlowPhase.Faulted) && GUILayout.Button("Match 종료 / 우주선 귀환"))
                    Run(() => flow.ReturnToSpaceshipAsync());
                if (flow.Phase != GameFlowPhase.MainMenu && GUILayout.Button("시작 화면으로")) Run(() => flow.OpenMainMenuAsync());
            }
            if (flow.LastFailure != null) EditorGUILayout.HelpBox(flow.LastFailure.Message, MessageType.Warning);
        }
        private static async void Run(Func<Task> operation)
        {
            try { await operation(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogWarning("Game flow 요청 실패: " + error.Message); }
        }
    }
}
