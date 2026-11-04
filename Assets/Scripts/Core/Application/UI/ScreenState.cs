// 화면 하나의 식별자와 표시 자료를 불변 형태로 보관합니다.
// 결과는 파괴된 actor나 Session을 다시 조회하지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class ScreenState
    {
        public long Id { get; }
        public ScreenKind Kind { get; }
        public MatchCompletion Completion { get; }
        public bool CanGoBack => Kind == ScreenKind.Menu;
        internal ScreenState(long id, ScreenKind kind, MatchCompletion completion = null)
        { Id = id; Kind = kind; Completion = completion; }
    }
}
