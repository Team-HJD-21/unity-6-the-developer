// Host의 전체 필드 터렛·전력·자동 큐를 하나의 월드 section으로 저장합니다.
// 종류별 전투 원본은 별도 계약에 맡기며 레벨 명령/전력 소비를 재실행하지 않습니다.
// 복원은 제작 catalog로 actor를 재생성한 뒤 전체 Feature의 타깃을 다시 연결합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretController : ICheckpointParticipant
    {
        private FieldTurretLevelUpgradeCatalog _checkpointCatalog;
        private FieldTurretUpgradeDefinition[] _checkpointUpgrades = Array.Empty<FieldTurretUpgradeDefinition>();
        string ICheckpointParticipant.Key => "world/field-turrets";
        int ICheckpointParticipant.Version => 1;
        CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
        int ICheckpointParticipant.RestoreOrder => 150;

        /// <summary>Core의 TurretMatchBinding이 실제 명령에 사용하는 같은 콘텐츠를 주입합니다.</summary>
        public void ConfigureCheckpointContent(FieldTurretLevelUpgradeCatalog catalog, IEnumerable<FieldTurretUpgradeDefinition> upgrades)
        {
            _checkpointCatalog = catalog;
            _checkpointUpgrades = upgrades?.ToArray() ?? Array.Empty<FieldTurretUpgradeDefinition>();
        }

        CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireCheckpointMatch(context);
            var state = CaptureWorld(codec);
            return codec.Write("world/field-turrets", 1, CheckpointSectionKind.World, writer => WriteWorld(writer, state, codec));
        }

        ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireCheckpointMatch(context.Current);
            var target = codec.Read(section, 1, reader => ReadWorld(reader, codec));
            ValidateWorld(target, codec);
            var backup = CaptureWorld(codec);
            // 최신 강화/용량은 유지하고 과거 위치·전투 상태만 복원합니다.
            target = MergeGrowth(target, CaptureGrowth(), MaximumPower, out var warnings);
            ValidateWorld(target, codec);
            var previousWarnings = _growthRestoreWarnings;
            return new CheckpointRestoreStep(() => { ApplyWorld(target, codec); _growthRestoreWarnings = warnings; },
                () => { ApplyWorld(backup, codec); _growthRestoreWarnings = previousWarnings; },
                resolveReferences: ResolveWorldReferences, resolveRollbackReferences: ResolveWorldReferences);
        }

        private void RequireCheckpointMatch(CheckpointCaptureContext context)
        {
            if (!HasWriteAuthority || _matchScope == null || context.Config.MatchId != _matchScope.MatchId || _checkpointCatalog == null)
                throw new InvalidOperationException("Bind the current authority turret Match and its checkpoint content catalog.");
        }

        private SavedWorld CaptureWorld(ICheckpointPayloadCodec codec)
        {
            var actors = ReadCheckpointActors().OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).Select(pair =>
            {
                if (pair.Value is not IFieldTurretCombatCheckpoint combat)
                    throw new NotSupportedException("Turret combat checkpoint is not implemented: " + pair.Value.GetType().Name);
                return new SavedActor(pair.Value.CaptureActorState(), combat.CaptureCombat(codec));
            }).ToArray();
            var world = new SavedWorld(CaptureControllerState(), actors);
            ValidateWorld(world, codec);
            return world;
        }

        private void ValidateWorld(SavedWorld world, ICheckpointPayloadCodec codec)
        {
            var ids = new HashSet<EntityId>(); long reserved = 0;
            foreach (var actor in world.Actors)
            {
                if (!ids.Add(actor.Common.EntityId)) throw new InvalidDataException("Duplicate saved turret identity.");
                var prefab = ResolvePrefab(actor.Common.Runtime.DefinitionId);
                prefab.ValidateActorState(actor.Common, ResolveUpgrade(actor.Common.Runtime.UpgradeId));
                if (prefab is not IFieldTurretCombatCheckpoint combat) throw new InvalidDataException("Saved turret has no combat writer.");
                combat.ValidateCombat(actor.Combat, codec);
                reserved += actor.Common.ReservedPower;
            }
            if (reserved != world.Controller.Budget.Reserved || world.Controller.Candidates.Any(item => !item.Completed && !ids.Contains(item.EntityId)))
                throw new InvalidDataException("Saved turret reservations or auto activation targets are inconsistent.");
        }

        /// <summary>종류별 writer가 이전 레벨에서 발사된 탄환의 제작 prefab을 복원할 때 사용합니다.</summary>
        public FieldTurretBase ResolveCheckpointPrefab(string definition) => ResolvePrefab(definition);

        private FieldTurretBase ResolvePrefab(string definition)
            => _checkpointCatalog.TryGetPrefab(definition, out var prefab) ? prefab : throw new InvalidDataException("Turret prefab not found: " + definition);

        private FieldTurretUpgradeDefinition ResolveUpgrade(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var matches = _checkpointUpgrades.Where(item => item != null && item.Id == id).Distinct().ToArray();
            return matches.Length == 1 ? matches[0] : throw new InvalidDataException("Turret upgrade not found or ambiguous: " + id);
        }

        private void ApplyWorld(SavedWorld state, ICheckpointPayloadCodec codec)
        {
            foreach (var old in ReadCheckpointActors().Values)
            {
                (old as IFieldTurretCombatCheckpoint)?.ReleaseCombat();
                old.DetachCheckpointReservation();
                CheckpointActorFactory.Retire(old.gameObject);
            }
            foreach (var saved in state.Actors)
            {
                var actor = FieldTurretFactory.CreateForCheckpoint(ResolvePrefab(saved.Common.Runtime.DefinitionId), saved.Common,
                    this, ResolveUpgrade(saved.Common.Runtime.UpgradeId));
                ((IFieldTurretCombatCheckpoint)actor).RestoreCombat(saved.Combat, codec);
            }
            RestoreControllerState(state.Controller);
        }

        private void ResolveWorldReferences()
        {
            foreach (var actor in ReadCheckpointActors().Values)
                ((IFieldTurretCombatCheckpoint)actor).ResolveCombatReferences();
            PublishCheckpointState();
        }

        private static void WriteWorld(BinaryWriter writer, SavedWorld world, ICheckpointPayloadCodec codec)
        {
            var c = world.Controller; var b = c.Budget;
            writer.Write(b.Maximum); writer.Write(b.Current); writer.Write(b.PendingRecovery); writer.Write(b.RecoveryElapsed);
            writer.Write(c.AutoActivationRemaining); writer.Write(c.AutoActivationRunning); writer.Write(c.Candidates.Count);
            foreach (var item in c.Candidates)
            {
                codec.WriteText(writer, item.EntityId.Value); writer.Write(item.EnemyCount); writer.Write(item.Score);
                writer.Write(item.Completed); writer.Write(item.FirepowerEfficiency);
            }
            writer.Write(world.Actors.Length);
            foreach (var actor in world.Actors)
            {
                WriteActor(writer, actor.Common, codec);
                codec.WriteText(writer, actor.Combat.Key); writer.Write(actor.Combat.Version);
                byte[] payload = actor.Combat.CopyPayload(); writer.Write(payload.Length); writer.Write(payload);
            }
        }

        private static SavedWorld ReadWorld(BinaryReader reader, ICheckpointPayloadCodec codec)
        {
            var budget = new FieldTurretPowerBudget.State(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadDouble());
            double remaining = reader.ReadDouble(); bool running = reader.ReadBoolean();
            int count = codec.ReadCount(reader, FieldTurretControllerState.MaximumCandidates);
            var candidates = new FieldTurretControllerState.Candidate[count];
            for (int i = 0; i < count; i++) candidates[i] = new FieldTurretControllerState.Candidate(new EntityId(codec.ReadText(reader)),
                reader.ReadInt32(), reader.ReadSingle(), reader.ReadBoolean(), reader.ReadSingle());
            var controller = new FieldTurretControllerState(budget, remaining, running, candidates);
            count = codec.ReadCount(reader, 4096); var actors = new SavedActor[count];
            for (int i = 0; i < count; i++)
            {
                var common = ReadActor(reader, codec); string key = codec.ReadText(reader); int version = reader.ReadInt32();
                int size = codec.ReadCount(reader, CheckpointSection.MaximumPayloadBytes); byte[] bytes = reader.ReadBytes(size);
                if (bytes.Length != size) throw new EndOfStreamException();
                actors[i] = new SavedActor(common, new CheckpointSection(key, version, CheckpointSectionKind.World, bytes));
            }
            return new SavedWorld(controller, actors);
        }

        private static void WriteActor(BinaryWriter w, FieldTurretActorState actor, ICheckpointPayloadCodec codec)
        {
            var r = actor.Runtime;
            codec.WriteText(w, actor.EntityId.Value); codec.WriteText(w, r.DefinitionId);
            w.Write(r.MaximumHealth); w.Write(r.CurrentHealth); w.Write(r.IsDestroyed); w.Write(r.IsActivated);
            w.Write(r.IsLocked); w.Write(r.IsTemporarilySuspended); w.Write(r.DamageBonus); codec.WriteText(w, r.UpgradeId);
            w.Write(r.UpgradeLevel); w.Write(r.DamageModifierRatio); w.Write(r.PowerModifierRatio); w.Write(r.RangeModifierRatio);
            w.Write(r.PendingRepairHealth); actor.Pose.Write(w); w.Write(actor.HasHead);
            var q = actor.HeadLocalRotation; w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
            w.Write(actor.FireElapsed); w.Write(actor.CoolingRemaining); w.Write(actor.CoolingDuration);
            w.Write(actor.CoolingInitialHeatRatio); w.Write(actor.PendingInitialActivation); w.Write(actor.ReservedPower);
        }

        private static FieldTurretActorState ReadActor(BinaryReader r, ICheckpointPayloadCodec codec)
        {
            var id = new EntityId(codec.ReadText(r));
            var state = new FieldTurretRuntimeState.State(codec.ReadText(r), r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean(),
                r.ReadBoolean(), r.ReadBoolean(), r.ReadInt32(), codec.ReadText(r), r.ReadInt32(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadDouble());
            var pose = CheckpointTransformState.Read(r); bool head = r.ReadBoolean();
            var rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            return new FieldTurretActorState(id, state, pose, head, rotation, r.ReadSingle(), r.ReadDouble(), r.ReadSingle(),
                r.ReadSingle(), r.ReadBoolean(), r.ReadInt32());
        }

        private sealed class SavedWorld
        {
            public readonly FieldTurretControllerState Controller;
            public readonly SavedActor[] Actors;
            public SavedWorld(FieldTurretControllerState controller, SavedActor[] actors) { Controller = controller; Actors = actors; }
        }
        private sealed class SavedActor
        {
            public readonly FieldTurretActorState Common;
            public readonly CheckpointSection Combat;
            public SavedActor(FieldTurretActorState common, CheckpointSection combat) { Common = common; Combat = combat; }
        }
    }
}
