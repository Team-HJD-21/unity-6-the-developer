// 현재 Encounter가 명시 선택한 생성기의 적·분대·소유 탄환을 저장합니다.
// 다른 테스트 생성기는 writer로 등록하지 않습니다. 복원은 스폰 예산/분배 이력에 가산하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

/// <summary>
/// 추적 중인 몬스터와 공격 투사체를 저장하고 정의 및 분대 참조를 복원한다.
/// </summary>
public partial class EnemySpawnExecutor
{
    private ICheckpointParticipant _checkpointWriter;
    internal ICheckpointParticipant CheckpointWriter => _checkpointWriter ??= new EnemyCheckpointWriter(this);
    internal FieldSimulationActor BindCheckpointProjectile(GameObject actor)
        => _fieldTime != null ? FieldSimulationActor.Attach(actor, _fieldTime, _gameplay) : null;
    private readonly List<(EnemyNetworkController actor, EnemyNetworkController.State state)> _restoredEnemies = new();
    private readonly List<(EnemySquad squad, string target)> _restoredSquads = new();

    private sealed class EnemyCheckpointWriter : ICheckpointParticipant
    {
        private readonly EnemySpawnExecutor _owner;
        public EnemyCheckpointWriter(EnemySpawnExecutor owner) => _owner = owner;
        public string Key => "world/enemies";
        public int Version => 2;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 160;
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            _owner.RequireCheckpointMatch(context.Config.MatchId);
            var state = _owner.CaptureWorld();
            return codec.Write(Key, Version, Kind, w => WriteWorld(w, state, codec));
        }
        /// <summary>
        /// 저장 데이터를 읽고 검증한 뒤 실제 적용과 되돌리기를 담당할 복원 단계를 준비한다.
        /// </summary>
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            _owner.RequireCheckpointMatch(context.Current.Config.MatchId);
            if (section.Version < 1 || section.Version > Version)
                throw new InvalidDataException("Unsupported enemy checkpoint version.");
            var target = codec.Read(section, section.Version, r => ReadWorld(r, codec, section.Version));
            _owner.ValidateWorld(target);
            var backup = _owner.CaptureWorld();
            return new CheckpointRestoreStep(() => _owner.ApplyWorld(target), () => _owner.ApplyWorld(backup),
                resolveReferences: _owner.ResolveCheckpointReferences, resolveRollbackReferences: _owner.ResolveCheckpointReferences);
        }
    }

    private void RequireCheckpointMatch(MatchId match)
    {
        if (!CanSpawn || _fieldTime == null || _fieldTime.Snapshot.MatchId != match || enemyList == null)
            throw new InvalidOperationException("Bind the current authority Enemy executor before checkpoint operations.");
    }

    private IEnumerable<EnemyDamageProjectile> CheckpointProjectiles()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var projectile in root.GetComponentsInChildren<EnemyDamageProjectile>())
                if (projectile.BelongsToCheckpoint(this))
                    yield return projectile;
    }

    /// <summary>
    /// 추적 중인 적과 공격 투사체, 분대 타깃 참조를 저장 자료로 모은다.
    /// </summary>
    private SavedWorld CaptureWorld()
    {
        var groups = new Dictionary<EnemySquad, int>();
        var actors = new List<SavedEnemy>();
        var targets = new List<string>();
        foreach (var enemy in _spawnedEnemies.Values.OrderBy(item => item.NetworkController.GetComponent<SceneEntityIdentity>().EntityId.Value, StringComparer.Ordinal))
        {
            if (!groups.TryGetValue(enemy.Squad, out int group))
            {
                groups.Add(enemy.Squad, group = groups.Count);
                targets.Add(CheckpointSceneReferences.Capture(enemy.Squad.CheckpointTarget));
            }
            actors.Add(new SavedEnemy(enemy.NetworkController.GetComponent<SceneEntityIdentity>().EntityId, enemy.EnemyType,
                enemy.SpawnPointId, group, enemy.NetworkController.CaptureCheckpoint()));
        }
        var projectiles = CheckpointProjectiles().Select(p => new SavedProjectile(p.CheckpointEnemyType, p.CaptureCheckpoint())).ToArray();
        var world = new SavedWorld(actors.ToArray(), targets.ToArray(), projectiles);
        ValidateWorld(world);
        return world;
    }

    private EnemyDefinition ResolveCheckpointEnemy(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || !enemyList.TryGetDefinition(type, out var definition) || definition.Prefab == null ||
            definition.Prefab.GetComponent<EnemyNetworkController>() == null)
            throw new InvalidDataException("Enemy content not found: " + type);
        return definition;
    }
    private EnemyDamageProjectile ResolveCheckpointProjectile(string type)
        => ResolveCheckpointEnemy(type).Prefab.GetComponent<EnemyProjectileAbility>()?.CheckpointProjectilePrefab ??
            throw new InvalidDataException("Enemy projectile content not found: " + type);

    /// <summary>
    /// 복원 전에 저장된 몬스터·투사체 정의와 상태의 유효성을 검사한다.
    /// </summary>
    private void ValidateWorld(SavedWorld world)
    {
        var ids = new HashSet<EntityId>();
        if (world.Enemies.Length > 16384 || world.Projectiles.Length > 16384 || world.Targets.Length > world.Enemies.Length)
            throw new InvalidDataException("Enemy checkpoint exceeds its storage bounds.");
        foreach (var saved in world.Enemies)
        {
            var prefab = ResolveCheckpointEnemy(saved.Type).Prefab;
            CheckpointActorFactory.ValidatePrefab(prefab, saved.Id);
            if (!ids.Add(saved.Id) || saved.Group < 0 || saved.Group >= world.Targets.Length ||
                prefab.GetComponent<EnemyHealth>()?.MaxHealth != saved.State.MaximumHealth ||
                (prefab.GetComponent<EnemyMovement>() != null) != (saved.State.Movement != null) ||
                (saved.State.Exploded && prefab.GetComponent<EnemyExplosionAbility>() == null))
                throw new InvalidDataException("Saved enemy identity, group or authored configuration changed.");
        }
        foreach (var saved in world.Enemies)
        {
            var ranged = ResolveCheckpointEnemy(saved.Type).Prefab.GetComponent<EnemyProjectileAbility>();
            if (saved.State.Ranged != null)
            {
                if (ranged == null)
                    throw new InvalidDataException("Saved ranged ability is missing from its prefab.");
                ranged.ValidateAttackState(saved.State.Ranged);
            }
            else if (ranged != null)
            {
                // v1은 Animation Event 발사 상태를 기록하지 않았으므로 해당 콘텐츠의 부분 복원을 금지합니다.
                ranged.ValidateAttackState(new EnemyProjectileAbility.AttackState(false, false, false, Vector2.zero, false, false));
            }
        }
        foreach (var projectile in world.Projectiles)
            _ = ResolveCheckpointProjectile(projectile.Type);
    }

    /// <summary>
    /// 현재 추적 개체를 정리하고 저장 자료에서 몬스터·투사체·분대를 복원한다.
    /// </summary>
    private void ApplyWorld(SavedWorld world)
    {
        foreach (var projectile in CheckpointProjectiles().ToArray())
            projectile.NetworkObject.Despawn(true);
        foreach (var old in _spawnedEnemies.Values.ToArray())
            if (old.NetworkObject != null && old.NetworkObject.IsSpawned)
                old.NetworkObject.Despawn(true);
        _spawnedEnemies.Clear();
        _squads.Clear();
        _restoredEnemies.Clear();
        _restoredSquads.Clear();
        for (int i = 0; i < world.Targets.Length; i++)
        {
            var savedMember = world.Enemies.FirstOrDefault(enemy => enemy.Group == i);
            var definition = savedMember != null ? ResolveCheckpointEnemy(savedMember.Type) : null;
            var squad = definition != null
                ? new EnemySquad(definition.SquadMode, definition.TargetRule)
                : new EnemySquad();
            _squads.Add(squad);
            _restoredSquads.Add((squad, world.Targets[i]));
        }
        foreach (var saved in world.Enemies)
        {
            // 저장 형식에 집계 플래그를 추가하지 않고 복원할 적의 Definition에서 다시 읽는다.
            EnemyDefinition definition = ResolveCheckpointEnemy(saved.Type);
            var actor = CheckpointActorFactory.Instantiate(definition.Prefab, gameObject.scene, saved.State.Pose, saved.Id);
            var network = actor.GetComponent<EnemyNetworkController>();
            var controller = actor.GetComponent<EnemyController>();
            network.CheckpointEnemyType = saved.Type;
            network.SetSimulationMatch(_fieldTime.Snapshot.MatchId, NetworkObjectId);
            var squad = _squads[saved.Group];
            controller.JoinSquad(squad, definition.IsIncludedInAttackerLimit);
            var ngo = actor.GetComponent<Unity.Netcode.NetworkObject>();
            ngo.Spawn();
            network.RestoreCheckpoint(saved.State);
            var context = new EncounterExecutionContext(_fieldTime.Snapshot.MatchId, 0, 0, EncounterRequestSource.Unspecified,
                spawnCancellation: _gameplay);
            _spawnedEnemies.Add(ngo.NetworkObjectId, new TrackedEnemy(ngo, controller, network, squad, context, saved.Type, saved.Point));
            network.Despawned += OnEnemyDespawned;
            network.DeathConfirmed += OnEnemyDied;
            _restoredEnemies.Add((network, saved.State));
        }
        foreach (var saved in world.Projectiles)
        {
            var projectile = Instantiate(ResolveCheckpointProjectile(saved.Type), saved.State.Pose.Position, saved.State.Pose.Rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(projectile.gameObject, gameObject.scene);
            projectile.RestoreCheckpoint(saved.State, this, saved.Type);
            projectile.NetworkObject.Spawn();
            // OnNetworkSpawn establishes server physics; reapply the saved pause-safe body afterward.
            projectile.RestoreCheckpoint(saved.State, this, saved.Type);
        }

    }

    private void ResolveCheckpointReferences()
    {
        foreach (var group in _restoredSquads)
            group.squad.RestoreCheckpointTarget(CheckpointSceneReferences.Resolve(gameObject.scene, group.target));
        foreach (var enemy in _restoredEnemies)
            enemy.actor.RestoreCheckpointReferences(enemy.state);
        _restoredSquads.Clear();
        _restoredEnemies.Clear();
    }

    /// <summary>
    /// 몬스터와 타깃·투사체 상태를 체크포인트 payload 형식으로 기록한다.
    /// </summary>
    private static void WriteWorld(BinaryWriter w, SavedWorld state, ICheckpointPayloadCodec codec)
    {
        w.Write(state.Targets.Length);
        foreach (string target in state.Targets)
            codec.WriteText(w, target);
        w.Write(state.Enemies.Length);
        foreach (var enemy in state.Enemies)
        {
            codec.WriteText(w, enemy.Id.Value);
            codec.WriteText(w, enemy.Type);
            codec.WriteText(w, enemy.Point);
            w.Write(enemy.Group);
            enemy.State.Write(w);
        }
        w.Write(state.Projectiles.Length);
        foreach (var projectile in state.Projectiles)
        {
            codec.WriteText(w, projectile.Type);
            projectile.State.Write(w);
        }
    }

    /// <summary>
    /// 체크포인트 버전에 맞춰 저장된 몬스터·타깃·투사체 자료를 읽는다.
    /// </summary>
    private static SavedWorld ReadWorld(BinaryReader r, ICheckpointPayloadCodec codec, int version)
    {
        var targets = new string[codec.ReadCount(r, 16384)];
        for (int i = 0; i < targets.Length; i++)
            targets[i] = codec.ReadText(r);
        var enemies = new SavedEnemy[codec.ReadCount(r, 16384)];
        for (int i = 0; i < enemies.Length; i++)
            enemies[i] = new SavedEnemy(new EntityId(codec.ReadText(r)), codec.ReadText(r), codec.ReadText(r),
            r.ReadInt32(), EnemyNetworkController.State.Read(r, codec, version));
        var projectiles = new SavedProjectile[codec.ReadCount(r, 16384)];
        for (int i = 0; i < projectiles.Length; i++)
            projectiles[i] = new SavedProjectile(codec.ReadText(r), EnemyDamageProjectile.State.Read(r));
        return new SavedWorld(enemies, targets, projectiles);
    }
    private sealed class SavedWorld
    {
        public readonly SavedEnemy[] Enemies; public readonly string[] Targets; public readonly SavedProjectile[] Projectiles;
        public SavedWorld(SavedEnemy[] enemies, string[] targets, SavedProjectile[] projectiles)
        {
            Enemies = enemies;
            Targets = targets;
            Projectiles = projectiles;
        }
    }
    private sealed class SavedEnemy
    {
        public readonly EntityId Id; public readonly string Type, Point; public readonly int Group; public readonly EnemyNetworkController.State State;
        public SavedEnemy(EntityId id, string type, string point, int group, EnemyNetworkController.State state)
        {
            Id = id;
            Type = type;
            Point = point;
            Group = group;
            State = state;
        }
    }
    private sealed class SavedProjectile
    {
        public readonly string Type; public readonly EnemyDamageProjectile.State State;
        public SavedProjectile(string type, EnemyDamageProjectile.State state)
        {
            Type = type;
            State = state;
        }
    }
}
