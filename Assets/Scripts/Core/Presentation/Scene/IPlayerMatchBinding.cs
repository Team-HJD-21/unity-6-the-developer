using TeamHJD.Game.Application;

namespace TeamHJD.Game.Presentation.Scene
{
    /// <summary>생성된 플레이어의 기능에 Scene이 소유한 Match를 전달합니다.</summary>
    public interface IPlayerMatchBinding
    {
        void BindMatch(MatchSession session);
        void UnbindMatch();
    }
}
