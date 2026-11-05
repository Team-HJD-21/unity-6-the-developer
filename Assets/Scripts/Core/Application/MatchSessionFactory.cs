// Match 범위 객체 묶음을 만들고 그 수명 관리를 호출자에게 넘깁니다.

using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchSessionFactory
    {
        public MatchSession Create(MatchConfig config, MatchState initialState, IModeRules modeRules)
        {
            return Create(config, initialState, modeRules, BattlefieldSpatialInput.Empty);
        }

        public MatchSession Create(
            MatchConfig config,
            MatchState initialState,
            IModeRules modeRules,
            BattlefieldSpatialInput battlefieldInput)
        {
            return Create(config, initialState, modeRules, battlefieldInput,
                BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
        }

        public MatchSession Create(
            MatchConfig config,
            MatchState initialState,
            IModeRules modeRules,
            BattlefieldSpatialInput battlefieldInput,
            BattlefieldDynamicSpatialInput dynamicBattlefieldInput,
            BattlefieldGridConfiguration battlefieldGridConfiguration)
        {
            return new MatchSession(config, initialState, modeRules,
                battlefieldInput, dynamicBattlefieldInput, battlefieldGridConfiguration);
        }
    }
}
