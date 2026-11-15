// 실제 적 피해를 점유 성과에 연결하는 읽기 사실입니다. 피해량은 오버킬을 제외합니다.
using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public enum EnemyDamageContributor { Unknown, Player, Turret }

    public sealed class EnemyDamageObservation
    {
        public MatchId MatchId { get; }
        public BattlefieldPoint Position { get; }
        public int Damage { get; }
        public EnemyDamageContributor Contributor { get; }
        public EnemyDamageObservation(MatchId matchId, BattlefieldPoint position, int damage, EnemyDamageContributor contributor)
        {
            if (matchId.IsEmpty || damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            MatchId = matchId; Position = position; Damage = damage; Contributor = contributor;
        }
    }

    public interface IEnemyCombatSource
    {
        event Action<EnemyDamageObservation> DamageObserved;

        /// <summary>권위 HP 원본이 확인한 실제 사망입니다. Despawn/Scene 정리는 이 사건을 발행하지 않습니다.</summary>
        event Action<MatchId, EntityId> DeathObserved;
    }
}
