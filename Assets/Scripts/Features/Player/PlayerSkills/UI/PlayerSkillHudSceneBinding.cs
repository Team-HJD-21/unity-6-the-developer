using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

/// <summary>같은 씬에서 늦게 생성되는 로컬 플레이어에게 기존 HUD를 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSkillHud))]
public sealed class PlayerSkillHudSceneBinding : MonoBehaviour
{
    [SerializeField] private string _localPlayerId = "local-player";
    [SerializeField, Min(.1f)] private float _retrySeconds = .25f;
    private PlayerSkillHud _hud;
    private float _nextRetry;

    private void Awake() => _hud = GetComponent<PlayerSkillHud>();
    private void OnEnable() => _nextRetry = 0;
    private void Update()
    {
        var current = _hud.Owner;
        if (current != null && !IsLocalPlayer(current)) _hud.Unbind();
        if (current != null && IsLocalPlayer(current) && HasIdentity(current)) return;
        if (Time.unscaledTime < _nextRetry) return;
        _nextRetry = Time.unscaledTime + _retrySeconds;
        var player = FindLocalPlayer();
        if (player == null) _hud.Unbind();
        else if (_hud.Owner != player) BindLocalPlayer(player);
    }

    /// <summary>생성 측에서 직접 연결할 때도 같은 로컬 소유자·씬 조건으로 검사합니다.</summary>
    public bool BindLocalPlayer(PlayerSkillController player)
    {
        if (_hud == null) _hud = GetComponent<PlayerSkillHud>();
        return isActiveAndEnabled && _hud.isActiveAndEnabled && IsLocalPlayer(player)
            && player.BindHud(_hud);
    }

    private bool IsLocalPlayer(PlayerSkillController player)
    {
        if (player == null || !player.isActiveAndEnabled || player.gameObject.scene != gameObject.scene) return false;
        var network = player.GetComponentInParent<NetworkObject>();
        if (network != null) return network.IsSpawned && network.IsOwner;
        var identity = player.GetComponent<PlayerActorIdentity>();
        return identity == null || identity.PlayerId.Value == _localPlayerId;
    }

    private static bool HasIdentity(PlayerSkillController player) =>
        player.GetComponentInParent<NetworkObject>() != null || player.GetComponent<PlayerActorIdentity>() != null;

    private PlayerSkillController FindLocalPlayer()
    {
        PlayerSkillController anonymous = null;
        int anonymousCount = 0;
        foreach (var player in FindObjectsByType<PlayerSkillController>(FindObjectsSortMode.None))
        {
            if (!IsLocalPlayer(player)) continue;
            if (HasIdentity(player)) return player;
            anonymous = player;
            anonymousCount++;
        }
        // PlayerTest 같은 단일 플레이어 씬에서는 ID가 없는 후보 하나만 허용합니다.
        return anonymousCount == 1 ? anonymous : null;
    }

    private void OnDisable() { if (_hud != null) _hud.Unbind(); }
}
