using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

/// <summary>EnemyHealth의 실제 사망 한 번을 전장 공유 XP로 전달합니다. 단순 Despawn에는 지급하지 않습니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class PlayerEnemyExperienceReward : NetworkBehaviour
{
    [Tooltip("임시 고정 처치 보상. 몬스터별 보상 연결 전까지 이 값을 공통 사용합니다.")]
    [SerializeField, Min(0)] private int _experienceReward = 10;
    private EnemyHealth _health;
    private bool _rewarded;

    private void OnEnable()
    {
        _health = GetComponent<EnemyHealth>();
        _rewarded = false;
        // OnNetworkSpawn의 사망→Despawn 구독보다 먼저 연결해 사망 RPC를 보낼 수 있게 합니다.
        _health.Died -= OnEnemyDied;
        _health.Died += OnEnemyDied;
    }
    private void OnDisable() { if (_health != null) _health.Died -= OnEnemyDied; }

    private void OnEnemyDied(EnemyHealth health)
    {
        if (health != _health || _rewarded || health.CurrentHealth > 0 || _experienceReward <= 0 ||
            !FieldSimulationTime.CanAdvance(this)) return;
        if (NetworkManager != null && NetworkManager.IsListening)
        {
            if (!IsSpawned || !IsServer) return;
            _rewarded = true;
            int reward = _experienceReward;
            string matchId = ResolveMatchId();
            ShareRewardRpc(reward, matchId);
            // Dedicated Server의 권위 Player 원본도 갱신합니다. Host는 RPC에서 중복 수신하지 않습니다.
            PlayerExperience.ShareEnemyExperience(gameObject, reward, matchId, true);
        }
        else
        {
            _rewarded = true;
            PlayerExperience.ShareEnemyExperience(gameObject, _experienceReward, ResolveMatchId(), false);
        }
    }

    private string ResolveMatchId()
    {
        var time = GetComponent<FieldSimulationActor>()?.TimeSource;
        return time != null ? time.Snapshot.MatchId.Value : GetComponent<EnemyNetworkController>()?.SimulationMatchId ?? "";
    }

    // 일반 피해·공용 포탑·플레이어 설치물 모두 EnemyHealth 사망으로 모입니다.
    // Client는 XP 요청을 보내지 않으며 서버가 확정한 보상만 같은 전장에 투영합니다.
    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void ShareRewardRpc(int amount, string matchId)
        => PlayerExperience.ShareEnemyExperience(gameObject, amount, matchId, true);
}
