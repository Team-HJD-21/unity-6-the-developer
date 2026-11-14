// Scene의 출입문이 Core에 요청하는 로컬 우주선 이동 경계입니다.
// 시작 허브 출격과 필드 진입 제한을 전달하며, 문은 Scene·Match 수명을 직접 관리하지 않습니다.
using System.Threading.Tasks;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public interface ISpaceshipTravel
    {
        bool IsTransitioning { get; }
        /// <summary>필드 출입구가 내부 진입 의도를 전달합니다. 현행 출격 규칙은 진입을 막고 공통 Alert로 안내합니다.</summary>
        Task EnterAsync(Transform player);
        /// <summary>시작 우주선 문이 제작된 Mission으로 출격합니다. 도착점은 전장의 PlayerStart가 소유합니다.</summary>
        Task ExitAsync(Transform player, string fieldScene);
    }
}
