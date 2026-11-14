// HUD/Debugger가 실제 점유율과 Field 사건·최종 보스 진행을 읽는 값입니다.
// 예정·생성 확인·처치를 구분하며 현재 점유 셀이나 Enemy 생명 원본을 소유하지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class StageProgressSnapshot
    {
        public MatchId MatchId { get; }
        public Guid RunId { get; }
        public double CaptureRatio { get; }
        public double HighestCaptureRatio { get; }
        public EntityId FirstFieldBoss { get; }
        public EntityId SecondFieldBoss { get; }
        public EntityId FinalBoss { get; }
        public bool FirstFieldBossDefeated { get; }
        public bool SecondFieldBossDefeated { get; }
        public bool FinalBossDefeated { get; }
        public string PendingReason { get; }
        public bool CanEnterFinalBattle => CaptureRatio >= 1 && !FirstFieldBoss.IsEmpty && !SecondFieldBoss.IsEmpty && FinalBoss.IsEmpty;
        public StageProgressSnapshot(MatchId match, Guid run, double current, double highest, EntityId first, EntityId second,
            EntityId final, bool firstDead, bool secondDead, bool finalDead, string pending)
        {
            MatchId = match; RunId = run; CaptureRatio = current; HighestCaptureRatio = highest;
            FirstFieldBoss = first; SecondFieldBoss = second; FinalBoss = final;
            FirstFieldBossDefeated = firstDead; SecondFieldBossDefeated = secondDead; FinalBossDefeated = finalDead;
            PendingReason = pending;
        }
    }
}
