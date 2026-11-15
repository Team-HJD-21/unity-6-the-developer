using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;

public sealed partial class PlayerSkillController : ILocalInputConsumer, IHubPlayerBinding
{
    // 개인 로컬 입력 scope가 같은 허브/필드 actor끼리 연구와 장착만 전달합니다.
    // 설치물·남은 수명·쿨타임은 기존 Match/체크포인트 소유를 유지합니다.
    private static readonly Dictionary<IInputBlocker, SessionState> SessionStates = new();
    private IInputBlocker _sessionInput;
    private bool IsRemoteSessionClient => Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsListening && !Unity.Netcode.NetworkManager.Singleton.IsServer;

    private sealed class SessionState
    {
        public PlayerSkillController Owner;
        public PlayerSkillResearch.Snapshot Research;
        public PlayerSkillId Q, E;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSessionStates() => SessionStates.Clear();

    private void OnEnable()
    {
        if (Research != null) Research.Changed += SaveSession;
        Loadout.Changed += SaveSession;
    }

    public void BindInputBlocker(IInputBlocker input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (ReferenceEquals(_sessionInput, input) && SessionStates.TryGetValue(input, out var bound) &&
            ReferenceEquals(bound.Owner, this)) return;
        SaveSession();
        _sessionInput = input;
        if (IsRemoteSessionClient) return;
        if (SessionStates.TryGetValue(input, out var saved))
        {
            if (Research != null && saved.Research != null) Research.Restore(saved.Research);
            Loadout.Restore(saved.Q, saved.E);
        }
        SessionStates[input] = new SessionState { Owner = this };
        SaveSession();
    }

    public void BindHub(IInputBlocker input) => BindInputBlocker(input);
    public void UnbindHub() { SaveSession(); _sessionInput = null; }

    private void SaveSession()
    {
        if (_sessionInput == null || IsRemoteSessionClient ||
            !SessionStates.TryGetValue(_sessionInput, out var state) || !ReferenceEquals(state.Owner, this)) return;
        state.Research = Research?.Capture();
        state.Q = Loadout.Q; state.E = Loadout.E;
    }

    private void ReleaseSessionSubscriptions()
    {
        SaveSession();
        if (Research != null) Research.Changed -= SaveSession;
        Loadout.Changed -= SaveSession;
    }
}
