// 필드 출입구의 Player 접촉을 Core 우주선 이동 API에 전달합니다.
// Scene 로드·로딩 화면·위치 보고·카메라 전환은 Core가 소유합니다.
using System;
using System.Threading.Tasks;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public class SpaceshipEnter : MonoBehaviour, ISpaceshipTravelConsumer
{
    [SerializeField] private string sceneToLoadName;
    [SerializeField] private string destinationName = "Capsule";
    private bool isTransitioning;
    private ISpaceshipTravel _travel;

    public void BindSpaceshipTravel(ISpaceshipTravel travel) => _travel = travel;

    private async void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerActorIdentity>() == null || isTransitioning || _travel?.IsTransitioning == true) return;
        try { await EnterAsync(other); }
        catch (Exception error) { if (this != null) Debug.LogException(error, this); }
    }

    /// <summary>필드의 Player가 접촉했을 때 호출합니다. 중복 충돌은 같은 이동을 다시 시작하지 않습니다.</summary>
    public async Task EnterAsync(Collider2D other)
    {
        if (isTransitioning || _travel?.IsTransitioning == true) return;
        if (_travel == null) throw new InvalidOperationException("Core 우주선 이동 API가 연결되지 않았습니다.");
        isTransitioning = true;
        try
        {
            // 현행 Player의 몸체 Collider는 자식의 PlayerBody 레이어에 있습니다.
            var identity = other.GetComponentInParent<PlayerActorIdentity>();
            if (identity == null) throw new ArgumentException("Core Player actor가 아닙니다.", nameof(other));
            Transform player = identity.transform;
            await _travel.EnterAsync(player, GetComponent<Collider2D>(), sceneToLoadName, destinationName);
        }
        finally { if (this != null) isTransitioning = false; }
    }
}
