// Scene 제작 참조를 App 소유 Encounter consumer에 명시적으로 전달합니다.
// 구체 Enemy 타입과 실행 대상 선택은 Feature가 소유합니다.
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterSceneConfiguration
    {
        void ConfigureEncounter(IEncounterMatchConsumer consumer);
    }
}
