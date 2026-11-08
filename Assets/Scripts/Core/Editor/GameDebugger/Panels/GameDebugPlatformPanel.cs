// Game Debugger에서 실제 플랫폼 초기화·사용자·2인 로비를 확인하는 개발 도구입니다.
// 초대 수락은 로비 참가 의도이며 NGO 연결/Match를 자동 시작하지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    internal static class GameDebugPlatformPanel
    {
        private static string _lobbyId = "", _message;
        private static bool _pending;
        private static IPlatformLobbyService _observed;
        public static void Draw()
        {
            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField("PLATFORM · STEAM", EditorStyles.boldLabel);
            var app = UnityEngine.Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AppRoot>() : null;
            var runtime = app?.Platform as IPlatformRuntime;
            if (GUILayout.Button("Steam 설정 선택 (Use Steam / AppID)"))
                Selection.activeObject = Resources.Load<UnityEngine.Object>("Core/SteamAppSettings");
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("현재는 개발용 플랫폼입니다. SteamAppSettings의 Use Steam을 선택하고 Play를 다시 시작하세요. 후보 AppID: 4336820.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("AppID / 초기화", runtime.ApplicationId + " / " + (runtime.IsInitialized ? "READY" : "NOT READY"));
            EditorGUILayout.LabelField("Steam 사용자", runtime.CurrentUser == null ? "미확인" : runtime.CurrentUser.DisplayName + " · " + runtime.CurrentUser.PlatformId);
            if (!string.IsNullOrEmpty(runtime.LastError))
                EditorGUILayout.HelpBox(runtime.LastError, MessageType.Warning);
            var lobbies = runtime as IPlatformLobbyService;
            if (!ReferenceEquals(_observed, lobbies))
            {
                if (_observed != null)
                    _observed.JoinRequested -= OnJoinRequested;
                _observed = lobbies;
                if (_observed != null)
                    _observed.JoinRequested += OnJoinRequested;
            }

            using (new EditorGUI.DisabledScope(_pending))
            {
                if (!runtime.IsInitialized && GUILayout.Button("Steam 초기화 다시 시도"))
                    Run(() => runtime.SignInAsync(CancellationToken.None));
                if (runtime.IsInitialized && lobbies != null)
                {
                    var lobby = lobbies.CurrentLobby;
                    _lobbyId = EditorGUILayout.TextField("참가할 Lobby ID", _lobbyId);
                    if (lobby == null)
                    {
                        if (GUILayout.Button("친구 공개 2인 로비 만들기"))
                            Run(() => lobbies.CreateLobbyAsync());
                        if (GUILayout.Button("입력한 로비 참가"))
                            Run(() => lobbies.JoinLobbyAsync(_lobbyId));
                    }
                    else
                    {
                        EditorGUILayout.SelectableLabel(lobby.LobbyId, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        EditorGUILayout.LabelField("소유자 / 인원", lobby.OwnerId + " / " + lobby.MemberIds.Count + " / 2");
                        if (GUILayout.Button("Steam 친구 초대 Overlay"))
                            Try(lobbies.OpenInviteOverlay);
                        if (GUILayout.Button("로비 나가기"))
                            Try(lobbies.LeaveLobby);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            EditorGUILayout.LabelField("로비와 NGO 게임 연결은 별도입니다. 이 버튼은 게임 서버를 열지 않습니다.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void OnJoinRequested(string lobbyId)
        {
            _lobbyId = lobbyId;
            _message = "초대 수신. 로비 ID를 확인하고 참가를 눌러주세요.";
        }

        private static void Try(Action action)
        {
            try
            {
                action();
                _message = null;
            }
            catch (Exception error)
            {
                _message = error.Message;
            }
        }

        private static async void Run(Func<Task> request)
        {
            _pending = true;
            try
            {
                await request();
                _message = "요청 완료";
            }
            catch (Exception error)
            {
                _message = error.Message;
            }
            finally
            {
                _pending = false;
            }
        }
    }
}
