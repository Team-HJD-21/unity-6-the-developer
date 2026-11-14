// 우주선 출구의 BoxCollider2D 충돌을 제작된 Mission 출격 요청으로 Core에 전달합니다.
// 시작 허브에서 필드가 아직 로드되지 않은 경우도 Core 게임 흐름으로 처리합니다.
using System;
using System.Threading.Tasks;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(BoxCollider2D))]
public class SpaceshipExit : MonoBehaviour, ISpaceshipTravelConsumer
{
    [SerializeField, FormerlySerializedAs("returnSceneName")] private string missionSceneName;
    private bool isTransitioning;
    private ISpaceshipTravel _travel;

    public void BindSpaceshipTravel(ISpaceshipTravel travel) => _travel = travel;

    /// <summary>출구에 닿은 Player의 출격을 요청합니다. Scene 교체 중 오브젝트가 파괴돼도 이동은 App이 마무리합니다.</summary>
    public async Task ExitAsync(Collider2D other)
    {
        if (isTransitioning || _travel?.IsTransitioning == true) return;
        if (_travel == null) throw new InvalidOperationException("Core 우주선 이동 API가 연결되지 않았습니다.");
        isTransitioning = true;
        try
        {
            var identity = other.GetComponentInParent<PlayerActorIdentity>();
            if (identity == null) throw new ArgumentException("Core Player actor가 아닙니다.", nameof(other));
            Transform player = identity.transform;
            await _travel.ExitAsync(player, missionSceneName);
        }
        finally { if (this != null) isTransitioning = false; }
    }

    private async void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<PlayerActorIdentity>() == null || isTransitioning || _travel?.IsTransitioning == true) return;
        try { await ExitAsync(collision.collider); }
        catch (Exception error) { if (this != null) Debug.LogException(error, this); }
    }
}
