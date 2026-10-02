// Scene 조립부가 AppRoot를 통해 Match 수명과 전장 입력을 관리하는 최소 앱 경계입니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public interface IAppMatchHost
    {
        /// <summary>새 Match를 만들고 시작한 세션을 호출자에게 반환합니다.</summary>
        MatchSession StartMatch(
            MatchConfig config,
            MatchState initialState,
            IModeRules modeRules,
            BattlefieldSpatialInput battlefieldInput,
            BattlefieldDynamicSpatialInput dynamicBattlefieldInput,
            BattlefieldGridConfiguration battlefieldGridConfiguration);

        /// <summary>Match가 보유한 동적 참가자 위치를 갱신합니다.</summary>
        void UpdateBattlefieldParticipants(BattlefieldDynamicSpatialInput dynamicInput);

        /// <summary>터렛 배치를 갱신하고 전장 topology를 다시 계산합니다.</summary>
        void UpdateBattlefieldTurretLayout(BattlefieldSpatialInput staticInput);

        /// <summary>현재 Match의 Grid 설정을 다시 적용합니다.</summary>
        void ReconfigureBattlefieldGrid(BattlefieldGridConfiguration configuration);

        /// <summary>현재 Match를 결과와 보상 영수증으로 완료합니다.</summary>
        MatchCompletion CompleteCurrentMatch(MatchOutcome outcome, long eventSequence);

        /// <summary>현재 Match를 종료하고 Match 범위 리소스를 폐기합니다.</summary>
        void EndCurrentMatch();
    }
}
