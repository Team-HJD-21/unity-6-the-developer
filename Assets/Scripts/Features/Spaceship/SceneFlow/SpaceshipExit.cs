// 우주선 문 애니메이션과 충돌을 유지하며 출격·필드 복귀 요청을 Core에 전달합니다.
// 시작 허브에서 필드가 아직 로드되지 않은 경우도 Core 게임 흐름으로 처리합니다.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class SpaceshipExit : MonoBehaviour, ISpaceshipTravelConsumer
{
    [SerializeField] private string returnSceneName;
    [SerializeField] private string originName;
    [SerializeField] private string returnTriggerName = "Enter";
    [SerializeField] private float returnPadding = 0.5f;
    private bool isTransitioning;
    private Animator _animator;
    private BoxCollider2D _blockingCollider;
    private ISpaceshipTravel _travel;
    private readonly HashSet<Collider2D> _nearPlayer = new();

    public void BindSpaceshipTravel(ISpaceshipTravel travel) => _travel = travel;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _blockingCollider = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerActorIdentity>() == null) return;
        _nearPlayer.Add(other);
        _animator.SetBool("nearPlayer", true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_nearPlayer.Remove(other)) _animator.SetBool("nearPlayer", _nearPlayer.Count > 0);
    }

    /// <summary>열린 문에 닿은 Player의 출격/복귀를 요청합니다. Scene 교체 중 문이 파괴돼도 이동은 App이 마무리합니다.</summary>
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
            await _travel.ExitAsync(player, returnSceneName, returnTriggerName, returnPadding);
        }
        finally { if (this != null) isTransitioning = false; }
    }

    /// <summary>기존 문 애니메이션 이벤트가 문 Collider를 전환합니다.</summary>
    public void ActivateDoorCollider(int active) => _blockingCollider.enabled = active == 0;

    private async void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<PlayerActorIdentity>() == null || isTransitioning || _travel?.IsTransitioning == true) return;
        try { await ExitAsync(collision.collider); }
        catch (Exception error) { if (this != null) Debug.LogException(error, this); }
    }
}
