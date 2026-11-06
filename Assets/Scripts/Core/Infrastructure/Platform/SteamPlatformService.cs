// App 범위에서 Steam SDK를 한 번 초기화하고 실제 사용자·콜백·종료를 관리합니다.
// Steam ID 조회는 Backend 인증/프로필 검증이나 NGO 연결을 대신하지 않습니다.
// 실제 Steam 실행 실패를 개발 Fake로 바꾸지 않습니다.
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using Steamworks;

namespace TeamHJD.Game.Infrastructure
{
    public sealed partial class SteamPlatformService : IPlatformRuntime, IPlatformLobbyService
    {
        private static SteamPlatformService _active;
        private readonly int _thread = System.Environment.CurrentManagedThreadId;
        private bool _disposed;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyChatUpdate_t> _membersChanged;
        private Callback<LobbyDataUpdate_t> _lobbyChanged;
        public string Provider => "steam";
        public uint ApplicationId { get; }
        public bool IsInitialized { get; private set; }
        public PlatformUser CurrentUser { get; private set; }
        public string LastError { get; private set; }

        public SteamPlatformService(uint appId)
        {
            if (appId == 0)
                throw new ArgumentOutOfRangeException(nameof(appId), "Set the actual Steam AppID.");
            ApplicationId = appId;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += BeforeEditorReload;
#endif
        }

        /// <summary>시작 화면/진단에서 현재 Steam 클라이언트의 계정을 확인합니다. 별도의 비밀번호 입력을 받지 않습니다.</summary>
        public Task<PlatformUser> SignInAsync(CancellationToken cancellationToken)
        {
            RequireThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (IsInitialized)
            {
                RequireOnline();
                return Task.FromResult(CurrentUser);
            }

            if (_active != null && !ReferenceEquals(_active, this))
                throw new InvalidOperationException("Another App already owns Steam SDK initialization.");
            try
            {
                var result = SteamAPI.InitEx(out var message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                    throw new InvalidOperationException($"Steam initialization failed ({result}): {message}");
                IsInitialized = true;
                _active = this;
                if (SteamUtils.GetAppID().m_AppId != ApplicationId)
                    throw new InvalidOperationException("Steam AppID differs from Core settings. Check steam_appid.txt or the Steam launch context.");
                RequireOnline();
                if (!SteamApps.BIsSubscribedApp(new AppId_t(ApplicationId)))
                    throw new InvalidOperationException("This Steam account does not have access to the configured AppID.");
                string id = SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
                CurrentUser = new PlatformUser(Provider, id, SteamFriends.GetPersonaName());
                _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(value => JoinRequested?.Invoke(value.m_steamIDLobby.ToString()));
                _membersChanged = Callback<LobbyChatUpdate_t>.Create(value =>
                {
                    if (CurrentLobby?.LobbyId == value.m_ulSteamIDLobby.ToString())
                        RefreshLobby();
                });
                _lobbyChanged = Callback<LobbyDataUpdate_t>.Create(value =>
                {
                    if (CurrentLobby?.LobbyId == value.m_ulSteamIDLobby.ToString())
                        RefreshLobby();
                });
                LastError = null;
                return Task.FromResult(CurrentUser);
            }
            catch (Exception error)
            {
                LastError = error.Message;
                ShutdownSdk();
                throw;
            }
        }

        public void Pump()
        {
            RequireThread();
            if (!IsInitialized)
                return;
            foreach (var call in _calls.ToArray())
                call.CheckDeadline();
            SteamAPI.RunCallbacks();
        }

        private void RequireOnline()
        {
            if (!SteamUser.BLoggedOn())
                throw new InvalidOperationException("Sign into the Steam client and connect it to Steam.");
        }

        private void RequireReady()
        {
            RequireThread();
            if (!IsInitialized)
                throw new InvalidOperationException("Initialize the Steam platform first.");
            RequireOnline();
        }

        private void RequireThread()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SteamPlatformService));
            if (System.Environment.CurrentManagedThreadId != _thread)
                throw new InvalidOperationException("Use Steam SDK on the App's owning Unity thread.");
        }

        private void ShutdownSdk()
        {
            _joinRequested?.Dispose();
            _joinRequested = null;
            _membersChanged?.Dispose();
            _membersChanged = null;
            _lobbyChanged?.Dispose();
            _lobbyChanged = null;
            if (IsInitialized)
                SteamAPI.Shutdown();
            IsInitialized = false;
            CurrentUser = null;
            if (ReferenceEquals(_active, this))
                _active = null;
        }

#if UNITY_EDITOR
        private void BeforeEditorReload()
        {
            try { Dispose(); }
            catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }
#endif
        public void Dispose()
        {
            if (_disposed)
                return;
            RequireThread();
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= BeforeEditorReload;
#endif
            try
            {
                foreach (var call in _calls.ToArray())
                    call.Dispose();
                _calls.Clear();
                if (IsInitialized && CurrentLobby != null)
                    SteamMatchmaking.LeaveLobby(new CSteamID(ulong.Parse(CurrentLobby.LobbyId, CultureInfo.InvariantCulture)));
                CurrentLobby = null;
                JoinRequested = null;
            }
            finally
            {
                ShutdownSdk();
                _disposed = true;
            }
        }
    }
}
