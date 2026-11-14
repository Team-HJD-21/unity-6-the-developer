// 실제 Boss Scene 준비 구현이 보고한 보스 ID와 도착 참가자를 불변 값으로 전달합니다.
// Core는 현재 Match/명단과 실제 Scene 개체를 다시 확인하며 이 값만으로 준비를 확정하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamHJD.Game.Domain
{
    public sealed class FinalBattleSceneReady
    {
        public MatchId MatchId { get; }
        public EntityId Boss { get; }
        public IReadOnlyList<PlayerId> ArrivedPlayers { get; }
        public FinalBattleSceneReady(MatchId match, EntityId boss, IEnumerable<PlayerId> arrivedPlayers)
        {
            if (match.IsEmpty || boss.IsEmpty) throw new ArgumentException("실제 Match와 보스 ID가 필요합니다.");
            var players = arrivedPlayers?.ToArray() ?? throw new ArgumentNullException(nameof(arrivedPlayers));
            if (players.Length == 0 || players.Any(p => p.IsEmpty) || players.Distinct().Count() != players.Length)
                throw new ArgumentException("도착 참가자는 비어 있거나 중복될 수 없습니다.", nameof(arrivedPlayers));
            MatchId = match; Boss = boss; ArrivedPlayers = Array.AsReadOnly(players);
        }
    }
}
