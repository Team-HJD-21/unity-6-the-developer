namespace TeamHJD.Game.Enemy.Contracts
{
    /// <summary>공격 출처에 따른 감속 허용 여부를 확인하고 적용 결과를 반환한다.</summary>
    public interface IAttackSlowEffectReceiver : ISlowEffectReceiver
    {
        /// <summary>수신자의 권한·생존·피격 규칙을 통과하면 감속을 적용한다.</summary>
        /// <param name="speedMultiplier">0 초과 1 미만의 속도 배율. 0.5는 50% 감속이다.</param>
        /// <param name="duration">양수인 게임 초 단위 지속시간.</param>
        /// <param name="source">확정한 공격 출처.</param>
        /// <returns>효과를 적용하거나 기존 효과 기한을 갱신했으면 true.</returns>
        bool TryApplySlow(float speedMultiplier, float duration, AttackSourceType source);
    }
}
