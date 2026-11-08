// Match가 없는 시작 우주선에서 이동·UI 입력만 허용하는 연결입니다.
// 실제 전투 Match를 만들거나 전투 명령의 권한을 주지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IHubPlayerBinding
    {
        void BindHub(IInputBlocker input);
        void UnbindHub();
    }
}
