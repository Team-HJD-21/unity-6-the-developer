// Processor가 수집 자료에서 계산한 스폰 후보·주변 플레이어·공격 기준점입니다.
// 출현 지점의 Region ID와 공격 기준점의 셀은 별개이며 예산은 Planner가 정합니다.
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterSituation
    {
        public EncounterCollection Collection { get; }
        public List<SpatialSpawnCandidate> Candidates { get; } = new List<SpatialSpawnCandidate>();
        public List<BattlefieldPoint> PlayerPositions { get; } = new List<BattlefieldPoint>();
        public Dictionary<string, BattlefieldPoint> TargetPositions { get; } = new Dictionary<string, BattlefieldPoint>();
        public Dictionary<string, TargetAttackType> TargetAttackTypes { get; } = new Dictionary<string, TargetAttackType>();
        public Dictionary<string, string> TargetNames { get; } = new Dictionary<string, string>();
        public List<SpawnDecisionSnapshot.Candidate> ExcludedPoints { get; } = new List<SpawnDecisionSnapshot.Candidate>();

        public EncounterSituation(EncounterCollection collection) => Collection = collection;
    }
}
