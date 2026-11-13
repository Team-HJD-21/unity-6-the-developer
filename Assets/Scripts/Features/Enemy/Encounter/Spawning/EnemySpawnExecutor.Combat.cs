// 생성기가 추적하는 적의 실제 피해만 같은 Match의 점유 관측자에게 전달합니다.
// 피해/공격 가능 규칙은 EnemyHealth에 두며 이 어댑터는 결과를 다시 계산하지 않습니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemy.Contracts;
using Unity.Netcode;
using UnityEngine;

public partial class EnemySpawnExecutor : IEnemyCombatSource
{
    public event Action<EnemyDamageObservation> DamageObserved;

    private void OnEnemyDamaged(EnemyHealth health, int damage, AttackSourceType source)
    {
        var network = health.GetComponent<NetworkObject>();
        if (!IsServer || network == null || !_spawnedEnemies.TryGetValue(network.NetworkObjectId, out var enemy) ||
            enemy.Context == null || enemy.Context.MatchId.IsEmpty || damage <= 0 || DamageObserved == null)
            return;
        var contributor = source == AttackSourceType.Player ? EnemyDamageContributor.Player :
            source == AttackSourceType.Turret ? EnemyDamageContributor.Turret : EnemyDamageContributor.Unknown;
        var position = health.transform.position;
        var observation = new EnemyDamageObservation(enemy.Context.MatchId, new BattlefieldPoint(position.x, position.y), damage, contributor);
        foreach (Action<EnemyDamageObservation> observer in DamageObserved.GetInvocationList())
            try { observer(observation); }
            catch (Exception error) { Debug.LogException(error, this); }
    }
}
