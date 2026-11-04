// Encounter가 계획 시점에 현재 Match 환경을 읽도록 명시적으로 연결합니다.
// 변경 알림 자체는 Spawn 명령이 아니며 Client 입력에 환경 snapshot을 넣지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterEnvironmentConsumer
    {
        void BindEnvironment(MatchId match, Func<MatchEnvironmentSnapshot> current);
        void UnbindEnvironment(MatchId match);
    }
}
