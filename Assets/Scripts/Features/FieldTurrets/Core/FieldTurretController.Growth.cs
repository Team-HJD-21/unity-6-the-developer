// Host 전장의 터렛 종류/강화와 CU 전력 용량을 월드 상태와 별개로 보존합니다.
// 과거 위치·HP비율·파괴·냉각은 유지하고 최신 강화 비용으로 전력을 다시 검증합니다.
// 신규 강화/소비 명령을 재실행하지 않으며 실제 Defense 생성·예약 원본만 사용합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using EntityId = TeamHJD.Game.Domain.EntityId;
namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretController
    {
        private string[] _growthRestoreWarnings = Array.Empty<string>();
        public IReadOnlyList<string> GrowthRestoreWarnings => Array.AsReadOnly(_growthRestoreWarnings);

        /// <summary>Core가 Host/캠페인/Stage 슬롯의 성장 저장에 등록합니다. 이벤트 구독 수명은 저장 scope가 소유합니다.</summary>
        public IGrowthCheckpointParticipant CreateGrowthParticipant() => new GrowthParticipant(this);

        private Dictionary<EntityId, TurretGrowth> CaptureGrowth()
            => ReadCheckpointActors().ToDictionary(pair => pair.Key, pair => new TurretGrowth(pair.Value.CaptureActorState().Runtime));

        // World는 과거 전투 상태, growth는 최신 영구 상태입니다. 읽기/검증 후 새 불변 계획을 만듭니다.
        private SavedWorld MergeGrowth(SavedWorld world, IReadOnlyDictionary<EntityId, TurretGrowth> growth,
            int capacity, out string[] warnings)
        {
            var messages = new List<string>();
            var activeNow = new HashSet<EntityId>(ReadCheckpointActors().Where(x => x.Value.IsActivated).Select(x => x.Key));
            long protectedPower = 0;
            foreach (var actor in world.Actors)
                if (actor.Common.Runtime.IsActivated && activeNow.Contains(actor.Common.EntityId) && growth.TryGetValue(actor.Common.EntityId, out var current))
                {
                    var definition = ResolvePrefab(current.Definition).Definition;
                    var upgrade = ResolveUpgrade(current.Upgrade);
                    protectedPower += FieldTurretRuntimeState.CalculateScaledStat(definition.Power,
                        upgrade == null ? 0 : upgrade.PowerModifierRatio * current.Rank);
                }
            int pending = Math.Min(Math.Max(0, capacity - (int)Math.Min(capacity, protectedPower)), world.Controller.Budget.PendingRecovery);
            int available = capacity - pending, reserved = 0;
            var result = new List<SavedActor>();
            var priorities = world.Controller.Candidates.Select((x, i) => (x.EntityId, i)).ToDictionary(x => x.EntityId, x => x.i);
            // 현재 정상 ON 개체를 먼저 보존합니다. 추가 복원 대상은 기존 CU 후보 순서/논리 ID 순서입니다.
            foreach (var saved in world.Actors.OrderByDescending(x => activeNow.Contains(x.Common.EntityId))
                .ThenBy(x => priorities.TryGetValue(x.Common.EntityId, out int i) ? i : int.MaxValue)
                .ThenBy(x => x.Common.EntityId.Value, StringComparer.Ordinal))
            {
                var old = saved.Common; var r = old.Runtime;
                if (!growth.TryGetValue(old.EntityId, out var latest)) throw new InvalidDataException("Latest turret growth is missing: " + old.EntityId);
                var definition = ResolvePrefab(latest.Definition).Definition;
                var upgrade = ResolveUpgrade(latest.Upgrade);
                var modifiers = latest.Rank;
                float damage = upgrade == null ? 0 : upgrade.DamageModifierRatio * modifiers;
                float power = upgrade == null ? 0 : upgrade.PowerModifierRatio * modifiers;
                float range = upgrade == null ? 0 : upgrade.RangeModifierRatio * modifiers;
                int health = RestoreHealthRatio(r.CurrentHealth, r.MaximumHealth, definition.MaxHealth, r.IsDestroyed);
                int cost = FieldTurretRuntimeState.CalculateScaledStat(definition.Power, power);
                bool active = r.IsActivated && cost <= available;
                if (r.IsActivated && !active) messages.Add(old.EntityId.Value + ": 최신 강화 전력 " + cost + " / 가용 " + available + " / OFF 복원");
                if (active) { available -= cost; reserved += cost; }
                var runtime = new FieldTurretRuntimeState.State(latest.Definition, definition.MaxHealth, health, r.IsDestroyed,
                    active, r.IsLocked, r.IsTemporarilySuspended && active, latest.DamageBonus, latest.Upgrade, latest.Rank,
                    damage, power, range, health > 0 && health < definition.MaxHealth ? r.PendingRepairHealth : 0);
                runtime.ValidateFor(definition, upgrade);
                var common = new FieldTurretActorState(old.EntityId, runtime, old.Pose, old.HasHead, old.HeadLocalRotation,
                    old.FireElapsed, old.CoolingRemaining, old.CoolingDuration, old.CoolingInitialHeatRatio,
                    old.PendingInitialActivation && !r.IsActivated, active ? cost : 0);
                result.Add(new SavedActor(common, saved.Combat));
            }
            var previous = world.Controller;
            var budget = new FieldTurretPowerBudget.State(capacity, capacity - pending - reserved, pending,
                pending > 0 ? previous.Budget.RecoveryElapsed : 0);
            warnings = messages.ToArray();
            return new SavedWorld(new FieldTurretControllerState(budget, previous.AutoActivationRemaining,
                previous.AutoActivationRunning, previous.Candidates), result.ToArray());
        }

        /// <summary>강화로 최대 HP가 달라져도 과거 전투 HP 비율을 유지합니다. 파괴 상태를 되살리지 않습니다.</summary>
        public static int RestoreHealthRatio(int health, int previousMaximum, int currentMaximum, bool destroyed)
        {
            if (previousMaximum <= 0 || currentMaximum <= 0 || health < 0 || health > previousMaximum)
                throw new ArgumentOutOfRangeException(nameof(health));
            return destroyed ? 0 : Math.Max(1, Math.Min(currentMaximum, (int)Math.Ceiling(health * (double)currentMaximum / previousMaximum)));
        }

        private sealed class TurretGrowth
        {
            public readonly string Definition, Upgrade;
            public readonly int Rank, DamageBonus;
            public TurretGrowth(FieldTurretRuntimeState.State state)
            { Definition = state.DefinitionId; Upgrade = state.UpgradeId; Rank = state.UpgradeLevel; DamageBonus = state.DamageBonus; }
            public TurretGrowth(string definition, string upgrade, int rank, int bonus)
            {
                if (string.IsNullOrWhiteSpace(definition) || rank < 0 || rank > FieldTurretRuntimeState.MaximumUpgradeLevel ||
                    (rank == 0) != string.IsNullOrEmpty(upgrade)) throw new InvalidDataException("Invalid saved turret growth.");
                Definition = definition; Upgrade = upgrade ?? ""; Rank = rank; DamageBonus = bonus;
            }
        }

        private sealed class GrowthParticipant : IGrowthCheckpointParticipant
        {
            private readonly FieldTurretController _owner;
            private event Action<GrowthSaveUrgency> _changed;
            private int _capacity;
            public GrowthParticipant(FieldTurretController owner) { _owner = owner; _capacity = owner.MaximumPower; }
            public string Key => "growth/field-turrets";
            public string PersistenceKey => "field-turrets";
            public int Version => 1;
            public int RestoreOrder => 150;
            public CheckpointSectionKind Kind => CheckpointSectionKind.Progression;
            public event Action<GrowthSaveUrgency> GrowthChanged
            {
                add { if (_changed == null) Subscribe(); _changed += value; }
                remove { _changed -= value; if (_changed == null) Unsubscribe(); }
            }
            private void Subscribe()
            {
                FieldTurretInstanceRegistry.UpgradeApplied += Upgrade;
                FieldTurretInstanceRegistry.UpgradeDowngraded += Upgrade;
                FieldTurretInstanceRegistry.LevelUpgraded += Level;
                FieldTurretInstanceRegistry.LevelDowngraded += Level;
                _owner.PowerChanged += Power;
            }
            private void Unsubscribe()
            {
                FieldTurretInstanceRegistry.UpgradeApplied -= Upgrade;
                FieldTurretInstanceRegistry.UpgradeDowngraded -= Upgrade;
                FieldTurretInstanceRegistry.LevelUpgraded -= Level;
                FieldTurretInstanceRegistry.LevelDowngraded -= Level;
                _owner.PowerChanged -= Power;
            }
            private void Upgrade(FieldTurretBase actor, FieldTurretUpgradeDefinition upgrade)
            { if (_owner.OwnsTurret(actor)) _changed?.Invoke(GrowthSaveUrgency.Immediate); }
            private void Level(FieldTurretBase previous, FieldTurretBase current)
            { if (_owner.OwnsTurret(current)) _changed?.Invoke(GrowthSaveUrgency.Immediate); }
            private void Power(int current, int maximum)
            {
                if (_capacity == maximum) return;
                _capacity = maximum; _changed?.Invoke(GrowthSaveUrgency.Immediate);
            }
            public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
            {
                _owner.RequireCheckpointMatch(context);
                var growth = _owner.CaptureGrowth();
                return codec.Write(Key, 1, Kind, w =>
                {
                    w.Write(_owner.MaximumPower); w.Write(growth.Count);
                    foreach (var pair in growth.OrderBy(x => x.Key.Value, StringComparer.Ordinal))
                    {
                        codec.WriteText(w, pair.Key.Value); codec.WriteText(w, pair.Value.Definition);
                        codec.WriteText(w, pair.Value.Upgrade); w.Write(pair.Value.Rank); w.Write(pair.Value.DamageBonus);
                    }
                });
            }
            public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
            {
                _owner.RequireCheckpointMatch(context.Current);
                var saved = codec.Read(section, 1, r =>
                {
                    int capacity = r.ReadInt32(); if (capacity < 0) throw new InvalidDataException("Invalid CU capacity.");
                    int count = codec.ReadCount(r, 4096); var states = new Dictionary<EntityId, TurretGrowth>();
                    for (int i = 0; i < count; i++)
                    {
                        var id = new EntityId(codec.ReadText(r));
                        if (id.IsEmpty || !states.TryAdd(id, new TurretGrowth(codec.ReadText(r), codec.ReadText(r), r.ReadInt32(), r.ReadInt32())))
                            throw new InvalidDataException("Duplicate/empty logical turret growth identity.");
                    }
                    return (capacity, states);
                });
                var backup = _owner.CaptureWorld(codec);
                if (backup.Actors.Length != saved.states.Count) throw new InvalidDataException("The authored turret set changed; growth migration is required.");
                var target = _owner.MergeGrowth(backup, saved.states, saved.capacity, out var warnings);
                _owner.ValidateWorld(target, codec);
                var previousWarnings = _owner._growthRestoreWarnings;
                return new CheckpointRestoreStep(() =>
                {
                    _owner.ApplyWorld(target, codec); _owner.ResolveWorldReferences();
                    _owner._growthRestoreWarnings = warnings; _capacity = saved.capacity;
                }, () =>
                {
                    _owner.ApplyWorld(backup, codec); _owner.ResolveWorldReferences();
                    _owner._growthRestoreWarnings = previousWarnings; _capacity = backup.Controller.Budget.Maximum;
                });
            }
        }
    }
}
