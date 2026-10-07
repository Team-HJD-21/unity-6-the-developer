// 요청의 계획·실제 생성 수와 결과 코드를 구분하는 불변 결과입니다. PlanId를 대신하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public readonly struct EncounterExecutionSummary
    {
        public int PlannedCount { get; }
        public int SpawnedCount { get; }
        public string ResultCode { get; }
        public SpawnDecisionSnapshot SpawnDecision { get; }
        public EncounterExecutionSummary(int plannedCount, int spawnedCount, string resultCode, SpawnDecisionSnapshot spawnDecision = null)
        { PlannedCount = plannedCount; SpawnedCount = spawnedCount; ResultCode = resultCode; SpawnDecision = spawnDecision; }
    }
}
