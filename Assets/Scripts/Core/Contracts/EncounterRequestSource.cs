// Encounter 요청의 입력 경로를 구분합니다. 별도의 자동/수동 모드는 아닙니다.
namespace TeamHJD.Game.Contracts
{
    public enum EncounterRequestSource { DebugWindow, RuntimeUI, SceneCollision, MatchStart, Unspecified, EnemySkill, MatchProduction, SituationEvent }
}
