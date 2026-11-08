// Scene의 출입문이 Core에 요청하는 로컬 우주선 이동 경계입니다.
// 시작 허브 출격과 진행 중 Match 방문을 구분하며, 문은 Scene·Match 수명을 직접 관리하지 않습니다.
using System.Threading.Tasks;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public interface ISpaceshipTravel
    {
        bool IsTransitioning { get; }
        /// <summary>필드 출입구가 기존 Match actor의 우주선 방문을 요청합니다. 입구 Collider는 안전한 복귀 위치 계산에 사용합니다.</summary>
        Task EnterAsync(Transform player, Collider2D entrance, string spaceshipScene, string destinationMarker);
        /// <summary>우주선 문이 첫 출격 또는 방문 중이던 필드로의 복귀를 요청합니다. 실제 Match 수명은 Core가 판단합니다.</summary>
        Task ExitAsync(Transform player, string fieldScene, string returnMarker, float padding);
    }
}
