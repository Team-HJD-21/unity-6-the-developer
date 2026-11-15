// 생성기가 소유한 적의 HP·물리·능력·경로·애니메이션 원본을 캡처합니다.
// NGO 실행 ID는 저장하지 않고 논리 개체 ID를 다음 실행에 이전합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>
/// 몬스터의 공통 상태를 저장하고 복원 후 타깃 참조를 다시 연결한다.
/// </summary>
public partial class EnemyNetworkController
{
    internal string CheckpointEnemyType
    {
        get; set;
    }

    /// <summary>
    /// 위치·물리·체력·능력·경로 상태를 모아 현재 몬스터의 체크포인트를 만든다.
    /// </summary>
    internal State CaptureCheckpoint()
    {
        if (!IsSpawned || !IsServer || enemyHealth.CurrentHealth <= 0)
            throw new InvalidOperationException("Capture a living authority enemy.");
        var ability = GetComponent<EnemyAbility>();
        var movement = GetComponent<EnemyMovement>();
        var body = GetComponent<Rigidbody2D>();
        if (_explosionAbility?.CheckpointExploded == true && !_explosionAbility.CheckpointRemovalPending)
        {
            // 피해 적용 직후 아직 Attack trigger가 소비되지 않은 프레임은 일관된 애니메이션 snapshot이 아닙니다.
            var animation = GetComponent<Animator>();
            bool hasCompletion = false;
            if (animation != null)
                foreach (var clip in animation.GetCurrentAnimatorClipInfo(0))
                    foreach (var entry in clip.clip.events)
                        hasCompletion |= entry.functionName == nameof(EnemyExplosionAbility.OnExplosionAnimationFinished);
            if (!hasCompletion)
                throw new InvalidOperationException("Retry after the enemy explosion animation starts.");
        }
        var ranged = GetComponent<EnemyProjectileAbility>()?.CaptureAttackState();
        if (ranged?.Executing == true && !ranged.FireDeferred && !ranged.FinishDeferred)
        {
            bool hasCompletion = false;
            var animation = GetComponent<Animator>();
            if (animation != null)
                foreach (var clip in animation.GetCurrentAnimatorClipInfo(0))
                    foreach (var entry in clip.clip.events)
                        hasCompletion |= entry.functionName == nameof(EnemyProjectileAbility.OnProjectileAttackAnimationFinished);
            if (!hasCompletion)
                throw new InvalidOperationException("Retry after the ranged attack animation starts.");
        }
        var simulation = GetComponent<FieldSimulationActor>() ?? throw new InvalidOperationException("Bind enemy simulation before capture.");
        return new State(CheckpointTransformState.Capture(transform), simulation.CaptureBody(body), enemyHealth.MaxHealth, enemyHealth.CurrentHealth,
            enemyHealth.CheckpointDamageActive, ability?.CheckpointCooldown ?? 0, _explosionAbility?.CheckpointExploded ?? false,
            movement?.CaptureCheckpoint(), CheckpointAnimatorState.Capture(GetComponent<Animator>()),
            ranged, _explosionAbility?.CheckpointRemovalPending ?? false);
    }

    /// <summary>
    /// 저장된 몬스터의 공통 상태를 적용하고 후속 참조 복원에 필요한 상태를 유지한다.
    /// </summary>
    internal void RestoreCheckpoint(State state)
    {
        state.Pose.Apply(transform);
        enemyHealth.RestoreCheckpoint(state.MaximumHealth, state.Health, state.DamageActive);
        GetComponent<EnemyAbility>()?.RestoreCheckpointCooldown(state.Cooldown);
        if (_explosionAbility != null)
            _explosionAbility.RestoreCheckpointExplosion(state.Exploded, state.RemovalPending);
        else if (state.Exploded)
            throw new InvalidDataException("Saved explosion does not match enemy abilities.");
        var ranged = GetComponent<EnemyProjectileAbility>();
        if (ranged != null)
            ranged.RestoreAttackState(state.Ranged ?? new EnemyProjectileAbility.AttackState(false, false, false, Vector2.zero, false, false));
        var body = GetComponent<Rigidbody2D>();
        var simulation = GetComponent<FieldSimulationActor>();
        if (simulation != null)
            simulation.RestoreBody(body, state.Body);
        else
            state.Body.Apply(body);
    }

    internal void RestoreCheckpointReferences(State state)
    {
        if (state.Movement != null)
            GetComponent<EnemyMovement>().RestoreCheckpoint(state.Movement);

        state.Animation.Apply(GetComponent<Animator>());

        var enemyAnimation = GetComponent<EnemyAnimation>();
        if (enemyAnimation != null)
            enemyAnimation.RefreshStateCache();
    }

    internal sealed class State
    {
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public readonly int MaximumHealth, Health;
        public readonly bool DamageActive, Exploded;
        public readonly double Cooldown;
        public readonly EnemyProjectileAbility.AttackState Ranged;
        public readonly bool RemovalPending;
        public readonly EnemyMovement.State Movement;
        public readonly CheckpointAnimatorState Animation;
        public State(CheckpointTransformState pose, CheckpointRigidbody2DState body, int maximum, int health, bool active,
            double cooldown, bool exploded, EnemyMovement.State movement, CheckpointAnimatorState animation,
            EnemyProjectileAbility.AttackState ranged, bool removalPending)
        {
            if (maximum <= 0 || health <= 0 || health > maximum || !double.IsFinite(cooldown) || cooldown < 0 || (exploded && active) || (removalPending && !exploded))
                throw new InvalidDataException("Invalid saved enemy state.");
            Pose = pose;
            Body = body;
            MaximumHealth = maximum;
            Health = health;
            DamageActive = active;
            Cooldown = cooldown;
            Exploded = exploded;
            Movement = movement;
            Animation = animation;
            Ranged = ranged;
            RemovalPending = removalPending;
        }
        public void Write(BinaryWriter w)
        {
            Pose.Write(w);
            Body.Write(w);
            w.Write(MaximumHealth);
            w.Write(Health);
            w.Write(DamageActive);
            w.Write(Cooldown);
            w.Write(Exploded);
            w.Write(Movement != null);
            Movement?.Write(w);
            Animation.Write(w);
            w.Write(Ranged != null);
            Ranged?.Write(w);
            w.Write(RemovalPending);
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec, int version) => new(CheckpointTransformState.Read(r), CheckpointRigidbody2DState.Read(r),
            r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadDouble(), r.ReadBoolean(), r.ReadBoolean() ? EnemyMovement.State.Read(r, codec) : null,
            CheckpointAnimatorState.Read(r, codec),
            version >= 2 && r.ReadBoolean() ? EnemyProjectileAbility.AttackState.Read(r) : null, version >= 2 && r.ReadBoolean());
    }
}
