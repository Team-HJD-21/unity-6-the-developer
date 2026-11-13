// 로컬 플레이어에게 잠깐 보여줄 결과와 의미색을 전달합니다.
// 로그·지속 상태·권한 판정·네트워크 동기화 데이터가 아닙니다.
namespace TeamHJD.Game.Application
{
    public enum AlertTone { Info, Success, Error }

    public sealed class AlertMessage
    {
        public string Text { get; }
        public string Key { get; }
        public AlertTone Tone { get; }

        internal AlertMessage(string text, AlertTone tone, string key)
        { Text = text; Tone = tone; Key = key; }
    }
}
