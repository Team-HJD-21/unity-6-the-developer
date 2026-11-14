// MatchCoordinator가 소유하는 Stage 진행입니다. 확정 점유 셀과 실제 생성 응답만 사용합니다.
// 33/66 사건을 누락·취소로 넘기지 않으며 두 실제 생성 확인 뒤에만 최종 진입을 허용합니다.
// 보스 콘텐츠·배치·컷씬·Scene 전환은 별도 Feature 원본이 수행합니다.
using System;
using System.Security.Cryptography;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class StageProgression
    {
        private readonly MatchId _match;
        private readonly Guid _run;
        private readonly IMatchClock _clock;
        private readonly Func<bool> _available;
        private readonly IFieldBossSpawnService _spawns;
        private readonly Guid _firstEvent, _secondEvent;
        private double _current, _highest, _nextAttempt;
        private EntityId _first, _second, _final;
        private bool _firstDead, _secondDead, _finalDead;
        private string _pending;
        public event Action Victory;
        public StageProgressSnapshot Snapshot => new(_match, _run, _current, _highest, _first, _second, _final,
            _firstDead, _secondDead, _finalDead, _pending);

        public StageProgression(MatchId match, Guid run, IMatchClock clock, Func<bool> available, IFieldBossSpawnService spawns)
        {
            if (match.IsEmpty || run == Guid.Empty) throw new ArgumentException("A current Mission identity is required.");
            _match = match; _run = run;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _available = available ?? throw new ArgumentNullException(nameof(available));
            _spawns = spawns;
            _firstEvent = EventId(run, 33); _secondEvent = EventId(run, 66);
            _pending = spawns == null ? "FieldBossContentNotConnected" : null;
        }

        /// <summary>Host Scene이 최신 확정 Grid를 전달합니다. 한 표본이 여러 임계값을 넘겨도 33→66 순서로 처리합니다.</summary>
        public void Pump(RegionGridSnapshot regions)
        {
            if (!_available() || regions == null || regions.MatchId != _match || !_final.IsEmpty) return;
            _current = regions.CaptureRatio; _highest = Math.Max(_highest, _current);
            if (_clock.ElapsedSeconds < _nextAttempt) return;
            if (_spawns == null) { _pending = "FieldBossContentNotConnected"; return; }
            if (_highest >= .33 && _first.IsEmpty && !TrySpawn(_firstEvent, 33, regions, ref _first)) return;
            if (_highest >= .66 && _second.IsEmpty && !TrySpawn(_secondEvent, 66, regions, ref _second)) return;
            _pending = _current >= 1 ? "FinalBossSceneAndPresentationRequired" : null;
        }
        private bool TrySpawn(Guid id, int threshold, RegionGridSnapshot regions, ref EntityId actor)
        {
            bool spawned = _spawns.TrySpawnFieldBoss(id, threshold, regions, out var actual, out string reason);
            if (!spawned || actual.IsEmpty)
            {
                _pending = string.IsNullOrEmpty(reason) ? "FieldBossSpawnUnconfirmed/" + threshold : reason;
                _nextAttempt = _clock.ElapsedSeconds + 1; // 기술 재시도만 제한하며 임계값 사건을 버리지 않습니다.
                return false;
            }
            if (actual == _first || actual == _second) throw new InvalidOperationException("Distinct Field events require distinct actual actors.");
            actor = actual; return true;
        }
        /// <summary>실제 최종 보스 Scene·연출 조립부가 같은 Match의 살아있는 보스 준비를 확인한 뒤 호출합니다.</summary>
        public void ConfirmFinalBossReady(EntityId actor)
        {
            if (!_available() || actor.IsEmpty || !Snapshot.CanEnterFinalBattle || actor == _first || actor == _second)
                throw new InvalidOperationException("Confirm one real final boss after both Field spawns and full capture.");
            _final = actor; _pending = null;
        }
        /// <summary>권위 Enemy 생명 관찰자가 실제 HP 사망 사실을 전달합니다. Despawn/Scene 해제는 이 API로 보내지 않습니다.</summary>
        public void ObserveEnemyDeath(EntityId actor)
        {
            if (!_available() || actor.IsEmpty) return;
            if (actor == _first) _firstDead = true;
            if (actor == _second) _secondDead = true;
            if (actor == _final && !_finalDead) { _finalDead = true; Victory?.Invoke(); }
        }

        internal void Restore(StageProgressSnapshot saved)
        {
            ValidateRestore(saved);
            _current = saved.CaptureRatio; _highest = saved.HighestCaptureRatio;
            _first = saved.FirstFieldBoss; _second = saved.SecondFieldBoss; _final = saved.FinalBoss;
            _firstDead = saved.FirstFieldBossDefeated; _secondDead = saved.SecondFieldBossDefeated; _finalDead = saved.FinalBossDefeated;
            _nextAttempt = 0; _pending = saved.PendingReason;
        }
        internal void ValidateRestore(StageProgressSnapshot saved)
        {
            if (saved == null || saved.RunId != _run || !double.IsFinite(saved.CaptureRatio) ||
                !double.IsFinite(saved.HighestCaptureRatio) || saved.CaptureRatio < 0 || saved.HighestCaptureRatio > 1 ||
                saved.HighestCaptureRatio < saved.CaptureRatio || (saved.FirstFieldBoss.IsEmpty && !saved.SecondFieldBoss.IsEmpty) ||
                (!saved.FinalBoss.IsEmpty && (saved.FirstFieldBoss.IsEmpty || saved.SecondFieldBoss.IsEmpty)) ||
                (!saved.FirstFieldBoss.IsEmpty && (saved.HighestCaptureRatio < .33 || saved.FirstFieldBoss == saved.SecondFieldBoss || saved.FirstFieldBoss == saved.FinalBoss)) ||
                (!saved.SecondFieldBoss.IsEmpty && (saved.HighestCaptureRatio < .66 || saved.SecondFieldBoss == saved.FinalBoss)) ||
                (!saved.FinalBoss.IsEmpty && saved.HighestCaptureRatio < 1) ||
                (saved.FirstFieldBoss.IsEmpty && saved.FirstFieldBossDefeated) || (saved.SecondFieldBoss.IsEmpty && saved.SecondFieldBossDefeated) ||
                (saved.FinalBoss.IsEmpty && saved.FinalBossDefeated))
                throw new InvalidOperationException("Invalid Stage progression restore.");
        }
        private static Guid EventId(Guid run, int percent)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(run.ToString("N") + "/field-boss/" + percent));
            var id = new byte[16]; Array.Copy(bytes, id, id.Length); return new Guid(id);
        }
    }
}
