// 제작 콘텐츠 선택을 불변 Match 설정으로 해석합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IContentCatalog
    {
        MatchConfig ResolveMatchConfig(MatchSelection selection);
    }
}
