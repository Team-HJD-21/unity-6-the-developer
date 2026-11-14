// 같은 요청 예산에서 떼어 둔 편성을 예고 후 그대로 실행하는 불변 계획입니다.
// 최종 위치는 Executor가 예고 영역 안에서 NavMesh·현재 객체 점유를 다시 검사합니다.
using System;

namespace TeamHJD.Game.Enemies.Encounter
{
    public sealed class EncounterDeferredSpawn
    {
        public SpawnInstruction Instruction { get; }
        public int Cost { get; }
        public double WarningSeconds { get; }
        public string Reason { get; }
        public EncounterDeferredSpawn(SpawnInstruction instruction, int cost, double warningSeconds, string reason)
        {
            if (instruction.Placement?.Area == null || cost < 1 || !double.IsFinite(warningSeconds) || warningSeconds < 0)
                throw new ArgumentException("A prepared area, positive cost and valid warning time are required.");
            Instruction = instruction; Cost = cost; WarningSeconds = warningSeconds; Reason = reason ?? "";
        }
    }

    internal interface IEncounterDeferredSpawnSink
    {
        bool CanShowWarning { get; }
        bool TrySchedulePrepared(EncounterDeferredSpawn plan, TeamHJD.Game.Contracts.EncounterExecutionContext cause);
    }
}
