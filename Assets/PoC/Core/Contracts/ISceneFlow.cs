// 앱 구성부가 요청하는 Scene 전환 기능의 계약을 정의합니다.

using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface ISceneFlow
    {
        Task LoadAsync(string sceneKey, CancellationToken cancellationToken);
        Task UnloadAsync(string sceneKey, CancellationToken cancellationToken);
    }
}
