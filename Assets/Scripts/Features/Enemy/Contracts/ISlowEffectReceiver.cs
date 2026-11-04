namespace TeamHJD.Game.Enemy.Contracts
{
    /// <summary>적 공격의 감속 효과를 받을 수 있는 대상의 계약이다.</summary>
    public interface ISlowEffectReceiver
    {
        /// <summary>대상에 맞는 속도를 지정한 시간 동안 낮춘다.</summary>
        /// <param name="speedMultiplier">원래 속도에 곱할 배율. 0.8이면 20% 감속이다.</param>
        /// <param name="duration">효과 지속시간(초).</param>
        void ApplySlow(float speedMultiplier, float duration);
    }
}
