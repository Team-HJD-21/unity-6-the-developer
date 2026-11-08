// Initial_Stage의 실제 서버 스폰과 타깃 연결, 이동을 검증합니다.
// Scene·NGO Host를 사용하는 명시적 테스트로 수동 재현과 같은 경로를 실행합니다.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Enemies.Targeting;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 스테이지에서 생성한 적의 유효 타깃 이동과 타깃 부재 동작을 검사한다.
/// </summary>
public sealed class EnemyMovementHotfixTests
{
    [Test]
    public void ControlUnitPrefab_UsesSharedDamageContractWithoutBecomingWeaponTurret()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FieldTurrets/ControlUnit.prefab");
        ControlUnitHealth health = prefab.GetComponent<ControlUnitHealth>();

        Assert.That(health, Is.Not.Null);
        Assert.That(health, Is.InstanceOf<IDamageableTarget>());
        Assert.That(health.Definition, Is.Not.Null);
        Assert.That(health.Definition.Role, Is.EqualTo(FieldTurretRole.ControlUnit));
        Assert.That(prefab.GetComponent<FieldTurretBase>(), Is.Null,
            "CU must stay outside activation, power and FieldTurretInstanceRegistry flows.");
        Assert.That(prefab.GetComponent<DamageableTargetAdapter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<TargetableComponent>(), Is.Not.Null);
    }

    [Test]
    public void InitialStagePlayerPrefab_ContainsActualHealthTargetAdapter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(prefab.GetComponent<PlayerTargetableAdapter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<TargetableComponent>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<TargetableComponent>().TargetType, Is.EqualTo(TargetType.Player));
    }

    [UnityTest]
    [Explicit("Initial_Stage Scene·NGO Host에서 이동 회귀를 확인할 때만 실행")]
    public IEnumerator InitialStage_SpawnedEnemiesMoveTowardsValidTargets()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();

        float deadline = Time.realtimeSinceStartup + 20f;
        EnemyController[] enemies;
        do
        {
            enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            if (enemies.Length > 0) break;
            yield return null;
        } while (Time.realtimeSinceStartup < deadline);

        var positions = new Dictionary<EnemyController, Vector3>();
        foreach (EnemyController enemy in enemies) positions.Add(enemy, enemy.transform.position);
        int validTargets = Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None)
            .Count(target => target.CanBeTargeted);
        int validPlayers = Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None)
            .Count(target => target.CanBeTargeted && target.TargetType == TargetType.Player);
        float start = Time.time;
        while (Time.time - start < 1f && Time.realtimeSinceStartup < deadline) yield return null;
        int moved = positions.Count(pair => pair.Key != null &&
            Vector2.Distance(pair.Value, pair.Key.transform.position) > 0.1f);
        Debug.Log($"Enemy movement diagnostic: spawned={enemies.Length}, validTargets={validTargets}, validPlayers={validPlayers}, moved={moved}");
        foreach (KeyValuePair<EnemyController, Vector3> pair in positions.Take(3))
        {
            if (pair.Key == null) continue;
            Transform target = pair.Key.GetComponent<EnemyTargeting>().Target;
            Rigidbody2D body = pair.Key.GetComponent<Rigidbody2D>();
            Debug.Log($"Enemy movement sample: target={(target == null ? "null" : target.name)}, " +
                $"positionDelta={Vector2.Distance(pair.Value, pair.Key.transform.position):F3}, rigidbodyVelocity={body.linearVelocity}. " +
                "이 AI는 NavMesh가 아니라 Transform XY 직접 이동을 사용합니다.");
        }

        // 터렛이 모두 꺼진 경우를 재현해 Player 연결 자체가 정지/이동을 좌우하는지 확인합니다.
        foreach (FieldTurretBase turret in Object.FindObjectsByType<FieldTurretBase>(FindObjectsSortMode.None))
            turret.enabled = false;
        foreach (DamageableTargetAdapter adapter in Object.FindObjectsByType<DamageableTargetAdapter>(FindObjectsSortMode.None))
            adapter.enabled = false;
        PlayerTargetableAdapter playerAdapter = Object.FindAnyObjectByType<PlayerTargetableAdapter>();
        Assert.That(playerAdapter, Is.Not.Null);
        playerAdapter.enabled = false;
        positions.Clear();
        foreach (EnemyController enemy in enemies)
            if (enemy != null) positions.Add(enemy, enemy.transform.position);
        start = Time.time;
        while (Time.time - start < 0.5f && Time.realtimeSinceStartup < deadline) yield return null;
        int movedWithoutTargets = positions.Count(pair => pair.Key != null &&
            Vector2.Distance(pair.Value, pair.Key.transform.position) > 0.1f);
        bool allTargetsCleared = positions.Keys.Where(enemy => enemy != null).All(enemy =>
            enemy.GetComponent<EnemyTargeting>().Target == null);
        playerAdapter.enabled = true;
        positions.Clear();
        foreach (EnemyController enemy in enemies)
            if (enemy != null) positions.Add(enemy, enemy.transform.position);
        start = Time.time;
        while (Time.time - start < 1f && Time.realtimeSinceStartup < deadline) yield return null;
        int movedWithPlayerOnly = positions.Count(pair => pair.Key != null &&
            Vector2.Distance(pair.Value, pair.Key.transform.position) > 0.1f);
        Debug.Log($"Enemy target regression: noCandidatesMoved={movedWithoutTargets}, playerOnlyMoved={movedWithPlayerOnly}");

        TargetableComponent playerTarget = Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None)
            .FirstOrDefault(target => target.TargetType == TargetType.Player);
        PlayerInfo playerHealth = playerTarget != null ? playerTarget.GetComponent<PlayerInfo>() : null;
        bool healthSynchronized = playerHealth != null && playerTarget.HealthRatio > 0f;
        int currentPlayerHealth = playerHealth != null ? playerHealth.curHp : 0;
        if (playerHealth != null)
        {
            // 기존 사망 루프를 호출하지 않고 타깃 어댑터의 체력/비활성 경계만 검증합니다.
            playerHealth.curHp = 0;
            playerHealth.onHpChange.Invoke(0, playerHealth.maxHp);
        }
        bool deadPlayerRejected = playerTarget != null && !playerTarget.CanBeTargeted;
        if (playerHealth != null)
        {
            playerHealth.curHp = currentPlayerHealth;
            playerHealth.onHpChange.Invoke(currentPlayerHealth, playerHealth.maxHp);
        }

        yield return new ExitPlayMode();
        Assert.That(enemies.Length, Is.GreaterThan(0), "첫 Encounter에서 실제 적이 생성되어야 합니다.");
        Assert.That(validPlayers, Is.GreaterThan(0), "Match Player는 터렛 활성 여부와 무관하게 유효한 추적 후보여야 합니다.");
        Assert.That(moved, Is.GreaterThan(0), "서버가 생성한 적 중 하나 이상이 실제 XY 위치를 변경해야 합니다.");
        Assert.That(allTargetsCleared, Is.True, "유효 타깃이 없으면 분대의 추적 목표를 해제해야 합니다.");
        Assert.That(movedWithPlayerOnly, Is.GreaterThan(0), "터렛이 꺼져도 살아 있는 Player를 추적해야 합니다.");
        Assert.That(healthSynchronized, Is.True, "Player 실제 체력이 타깃 상태에 전달되어야 합니다.");
        Assert.That(deadPlayerRejected, Is.True, "체력이 소진된 Player는 추적 후보에서 제외되어야 합니다.");
    }
}
