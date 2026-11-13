// 일반 Encounter와 Enemy 스킬 소환의 실제 생성·분대·NGO·관측 등록을 한곳에서 수행합니다.
// 편성이나 예산은 다시 결정하지 않으며, 실패한 생성 객체는 분대와 네트워크에서 정리합니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class EnemySpawnExecutor
{
    private bool TrySpawnActor(EnemyDefinition definition, Vector3 position, Quaternion rotation,
        EnemySquad squad, EncounterExecutionContext context, string pointId,
        Action<SpawnDecisionSnapshot.Actual> observeSpawn = null, Action<string> observeFailure = null)
    {
        if (!CanSpawn || !CanSimulateField || context?.SpawnCancellation.IsCancellationRequested == true)
        {
            Fail("Match/source is no longer running.", observeFailure);
            return false;
        }

        GameObject actor = Instantiate(definition.Prefab, position, rotation);
        EnemyController controller = actor.GetComponent<EnemyController>();
        NetworkObject network = actor.GetComponent<NetworkObject>();
        bool joined = false;
        try
        {
            if (actor.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(actor, gameObject.scene);
            // Spawn 통지 전에 소속·식별자·시계를 지정해야 Host와 Client가 같은 최초 상태를 받습니다.
            joined = controller.JoinSquad(squad, definition.IsIncludedInAttackerLimit);
            if (!joined)
            {
                Destroy(actor);
                Fail("Failed to register squad member: " + definition.EnemyId, observeFailure);
                return false;
            }
            CheckpointActorFactory.EnsureIdentity(actor);
            var simulation = actor.GetComponent<EnemyNetworkController>();
            simulation.CheckpointEnemyType = definition.EnemyId;
            simulation.SetSimulationMatch(context?.MatchId ?? _fieldTime?.Snapshot.MatchId ?? default, NetworkObjectId);
            network.Spawn();
            _spawnedEnemies.Add(network.NetworkObjectId,
                new TrackedEnemy(network, controller, simulation, squad, context, definition.EnemyId, pointId));
            simulation.Despawned += OnEnemyDespawned;
            simulation.DeathConfirmed += OnEnemyDied;
            var health = actor.GetComponent<EnemyHealth>();
            if (health != null) health.DamageApplied += OnEnemyDamaged;
            if (observeSpawn != null)
                try
                {
                    observeSpawn(new SpawnDecisionSnapshot.Actual(pointId, definition.EnemyId,
                        actor.GetComponent<SceneEntityIdentity>().EntityId.Value,
                        new BattlefieldPoint(actor.transform.position.x, actor.transform.position.y)));
                }
                catch (Exception error) { Debug.LogException(error, this); }
            Observe(_spawnedEnemies[network.NetworkObjectId], GameDiagnosticKind.EnemySpawned, "Network spawn succeeded");
            return true;
        }
        catch
        {
            if (network != null && network.IsSpawned) network.Despawn(true);
            else if (actor != null) Destroy(actor);
            if (joined) squad.RemoveMember(controller);
            throw;
        }
    }
}
