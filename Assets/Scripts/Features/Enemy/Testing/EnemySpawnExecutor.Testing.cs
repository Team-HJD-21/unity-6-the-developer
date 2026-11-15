#if UNITY_EDITOR
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;

public partial class EnemySpawnExecutor
{
    /// <summary>Editor 기능 시험에서 한 종류를 1마리 생성한다. 공통 생성·시간 연결·추적·분대 처리는 Execute에 맡긴다.</summary>
    public bool TrySpawnTestEnemy(EnemyDefinition definition, SpawnPoint point, TargetableComponent initialTarget,
        EncounterExecutionContext context, out EnemyController controller, out string result,
        EnemyTargetRule targetRule = EnemyTargetRule.PlayerOrTurret)
    {
        controller = null;
        result = "Host·현재 Match·같은 씬의 유효한 출현 지점과 카탈로그 정의가 필요합니다.";
        if (!Enum.IsDefined(typeof(EnemyTargetRule), targetRule) ||
            !CanSpawn || !CanSimulateField || context == null || context.MatchId.IsEmpty ||
            context.SpawnCancellation.IsCancellationRequested || point == null ||
            point.gameObject.scene != gameObject.scene || !point.CanSpawnEnemies || definition == null ||
            enemyList == null || !enemyList.TryGetDefinition(definition.EnemyId, out var registered) || registered != definition)
            return false;

        if (initialTarget != null && (!initialTarget.CanBeTargeted ||
            initialTarget.gameObject.scene != gameObject.scene ||
            (targetRule == EnemyTargetRule.PlayerOnly && initialTarget.TargetType != TargetType.Player) ||
            (targetRule == EnemyTargetRule.TurretOnly && initialTarget.TargetType != TargetType.Turret) ||
            (definition.IsIncludedInAttackerLimit && initialTarget.MaxAttackers > 0 &&
             initialTarget.AssignedAttackerCount >= initialTarget.MaxAttackers)))
        {
            result = "초기 타깃의 씬·타깃 가능 상태·유형·공격 인원 수용량을 확인하세요. PlayerOnly는 플레이어만, TurretOnly는 시설만 지정할 수 있습니다.";
            return false;
        }

        RefreshSpawnPoints();
        var instruction = new SpawnInstruction("IndividualTest", point.SpawnPointId,
            new[] { new EnemySpawnEntry(definition.EnemyId, 1) }, targetRule: targetRule);
        string failure = null;
        int count = Execute(instruction, context, null, reason => failure = reason);
        if (count != 1) { result = failure ?? "몬스터가 생성되지 않았습니다."; return false; }

        // 같은 호출의 생성 기록에서 분대를 찾아 초기 타깃을 연결한다.
        // 기존 분대의 배정 반환·새 배정 확보를 함께 수행하는 공통 복원 경로를 재사용한다.
        foreach (var tracked in _spawnedEnemies.Values)
        {
            if (!ReferenceEquals(tracked.Context, context)) continue;
            controller = tracked.Controller;
            if (initialTarget != null)
            {
                tracked.Squad.RestoreCheckpointTarget(initialTarget.transform);
                if (tracked.Squad.CheckpointTarget != initialTarget.transform)
                {
                    tracked.NetworkObject.Despawn(true);
                    controller = null;
                    result = "지정한 초기 타깃 배정에 실패해 생성한 몬스터를 정리했습니다.";
                    return false;
                }
            }
            result = $"{definition.EnemyId} 1마리 생성 · 지점 {point.name} · 초기 타깃 {(initialTarget != null ? initialTarget.name : "분대 자동 선택")}";
            return true;
        }
        result = "생성 기록을 확인할 수 없습니다.";
        return false;
    }
}
#endif
