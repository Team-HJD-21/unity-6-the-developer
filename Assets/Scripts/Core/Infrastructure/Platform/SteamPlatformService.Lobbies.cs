// Steam 2인 로비와 초대 API를 연결하고, 취소된 요청도 SDK 응답 정리 후 다음 요청을 받습니다.
// 로비 참가 이후 NGO transport/Match 시작은 별도의 연결 단계입니다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Steamworks;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Infrastructure
{
    public sealed partial class SteamPlatformService
    {
        private const string ProtocolKey = "teamhjd.protocol", Protocol = "1";
        private readonly List<ILobbyCall> _calls = new List<ILobbyCall>();
        public PlatformLobby CurrentLobby { get; private set; }

        public event Action<string> JoinRequested;
        /// <summary>우주선 접속 UI/진단이 친구 공개 2인 로비를 만듭니다. Host 서버를 자동 시작하지 않습니다.</summary>
        public Task<PlatformLobby> CreateLobbyAsync(CancellationToken cancellation = default)
        {
            BeforeLobbyRequest(cancellation);
            return BeginCall<LobbyCreated_t>(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2), value =>
            {
                if (value.m_eResult != EResult.k_EResultOK)
                    throw new InvalidOperationException("Steam lobby creation: " + value.m_eResult);
                return value.m_ulSteamIDLobby;
            }, true, cancellation);
        }

        public Task<PlatformLobby> JoinLobbyAsync(string lobbyId, CancellationToken cancellation = default)
        {
            BeforeLobbyRequest(cancellation);
            if (!ulong.TryParse(lobbyId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !new CSteamID(id).IsValid())
                throw new ArgumentException("Provide a valid Steam lobby ID.", nameof(lobbyId));
            return BeginCall<LobbyEnter_t>(SteamMatchmaking.JoinLobby(new CSteamID(id)), value =>
            {
                if (value.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    throw new InvalidOperationException("Steam lobby entry: " + value.m_EChatRoomEnterResponse);
                return value.m_ulSteamIDLobby;
            }, false, cancellation);
        }

        private void BeforeLobbyRequest(CancellationToken cancellation)
        {
            RequireReady();
            cancellation.ThrowIfCancellationRequested();
            if (CurrentLobby != null)
                throw new InvalidOperationException("Leave the current lobby before creating/joining another.");
            // Task 취소/시간 초과는 SDK 참가를 취소하지 않습니다. 이전 응답이 새 참가를 나가게 하지 않도록 직렬화합니다.
            if (_calls.Count != 0)
                throw new InvalidOperationException("A Steam lobby request is still awaiting its SDK callback/cleanup. Keep pumping callbacks before retrying.");
        }

        private Task<PlatformLobby> BeginCall<T>(SteamAPICall_t handle, Func<T, ulong> resolve, bool create, CancellationToken cancellation)
        {
            if (handle == SteamAPICall_t.Invalid)
                throw new InvalidOperationException("Steam did not accept the lobby request.");
            var call = new LobbyCall<T>(this, handle, resolve, create, cancellation);
            _calls.Add(call);
            return call.Task;
        }

        public void LeaveLobby()
        {
            RequireReady();
            // 진행 중 요청도 포기 처리합니다. 늦은 SDK 응답의 로비를 정리할 때까지 새 요청은 받지 않습니다.
            foreach (var call in _calls.ToArray())
                call.Abandon();
            if (CurrentLobby == null)
                return;
            SteamMatchmaking.LeaveLobby(new CSteamID(ulong.Parse(CurrentLobby.LobbyId, CultureInfo.InvariantCulture)));
            CurrentLobby = null;
        }

        public void OpenInviteOverlay()
        {
            RequireReady();
            if (CurrentLobby == null)
                throw new InvalidOperationException("Create or join a lobby first.");
            if (!SteamUtils.IsOverlayEnabled())
                throw new InvalidOperationException("Steam overlay is unavailable. Verify the standalone build launched with Steam.");
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(ulong.Parse(CurrentLobby.LobbyId, CultureInfo.InvariantCulture)));
        }

        private PlatformLobby AcceptLobby(ulong id, bool created)
        {
            var lobby = new CSteamID(id);
            try
            {
                if (created && !SteamMatchmaking.SetLobbyData(lobby, ProtocolKey, Protocol))
                    throw new InvalidOperationException("Could not publish the lobby protocol.");
                if (SteamMatchmaking.GetLobbyData(lobby, ProtocolKey) != Protocol || SteamMatchmaking.GetNumLobbyMembers(lobby) > 2)
                    throw new InvalidOperationException("The lobby version or participant count is incompatible.");
                CurrentLobby = ReadLobby(lobby);
                return CurrentLobby;
            }
            catch
            {
                SteamMatchmaking.LeaveLobby(lobby);
                throw;
            }
        }

        private void RefreshLobby()
        {
            if (CurrentLobby == null)
                return;
            var lobby = new CSteamID(ulong.Parse(CurrentLobby.LobbyId, CultureInfo.InvariantCulture));
            CurrentLobby = ReadLobby(lobby);
        }

        private static PlatformLobby ReadLobby(CSteamID lobby)
        {
            var members = new List<string>();
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int i = 0; i < count; i++)
                members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).ToString());
            return new PlatformLobby(lobby.ToString(), SteamMatchmaking.GetLobbyOwner(lobby).ToString(), members);
        }

        private interface ILobbyCall : IDisposable
        {
            void CheckDeadline();
            void Abandon();
        }

        // SDK 호출 자체는 취소되지 않습니다. Task만 끝내도 callback 참조는 유지해야 늦게 생긴 로비를 정리할 수 있습니다.
        private sealed class LobbyCall<T> : ILobbyCall
        {
            private readonly SteamPlatformService _owner;
            private readonly Func<T, ulong> _resolve;
            private readonly bool _created;
            private readonly CancellationToken _cancellation;
            private readonly long _deadline = Stopwatch.GetTimestamp() + 20 * Stopwatch.Frequency;
            private readonly TaskCompletionSource<PlatformLobby> _result = new TaskCompletionSource<PlatformLobby>();
            private CallResult<T> _callback;
            public Task<PlatformLobby> Task => _result.Task;

            public LobbyCall(SteamPlatformService owner, SteamAPICall_t handle, Func<T, ulong> resolve, bool created, CancellationToken cancellation)
            {
                _owner = owner;
                _resolve = resolve;
                _created = created;
                _cancellation = cancellation;
                _callback = CallResult<T>.Create(Complete);
                _callback.Set(handle);
            }

            public void CheckDeadline()
            {
                if (_cancellation.IsCancellationRequested)
                    _result.TrySetCanceled(_cancellation);
                else if (Stopwatch.GetTimestamp() >= _deadline)
                    _result.TrySetException(new TimeoutException("Steam lobby request timed out."));
            }

            public void Abandon() => _result.TrySetCanceled();
            private void Complete(T value, bool ioFailure)
            {
                PlatformLobby lobby = null;
                Exception failure = null;
                try
                {
                    CheckDeadline();
                    if (ioFailure)
                        throw new InvalidOperationException("Steam lobby request failed during transport.");
                    ulong id = _resolve(value);
                    if (Task.IsCompleted)
                    {
                        SteamMatchmaking.LeaveLobby(new CSteamID(id));
                        return;
                    }

                    lobby = _owner.AcceptLobby(id, _created);
                }
                catch (Exception error)
                {
                    _owner.LastError = error.Message;
                    failure = error;
                }
                finally
                {
                    _callback?.Dispose();
                    _callback = null;
                    _owner._calls.Remove(this);
                }

                // 완료를 기다리던 호출부가 즉시 나가기/재요청해도 SDK 대기 슬롯은 이미 정리된 상태여야 합니다.
                if (failure != null)
                    _result.TrySetException(failure);
                else if (lobby != null)
                    _result.TrySetResult(lobby);
            }

            public void Dispose()
            {
                Abandon();
                _callback?.Dispose();
                _callback = null;
            }
        }
    }
}
