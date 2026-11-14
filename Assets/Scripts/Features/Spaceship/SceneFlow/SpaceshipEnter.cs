// 필드 출입구의 Player 접촉을 Core 진입 제한 API에 전달합니다.
// 문 연출과 접촉 추적을 담당하며 Scene을 직접 생성하거나 Player를 이동시키지 않습니다.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public class SpaceshipEnter : MonoBehaviour, ISpaceshipTravelConsumer
{
    private bool isTransitioning;
    private Animator _animator;
    private BoxCollider2D _blockingCollider;
    private ISpaceshipTravel _travel;

    private readonly HashSet<Collider2D> _nearPlayer = new();

    public void BindSpaceshipTravel(ISpaceshipTravel travel) => _travel = travel;

    private void Awake()
    {
        _animator = GetComponentInParent<Animator>();
        _blockingCollider = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerActorIdentity>() == null) return;
        _nearPlayer.Add(other);
        // Graybox 출입구는 문 Animator가 없을 수 있습니다. 이동 API와 접촉 추적은 그대로 유지합니다.
        if (_animator != null) _animator.SetBool("NearPlayer", true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_nearPlayer.Remove(other) && _animator != null) _animator.SetBool("NearPlayer", _nearPlayer.Count > 0);
    }

    /// <summary>필드 Player의 접촉을 Core에 전달합니다. 출격 중 내부 진입은 공통 Alert로 안내합니다.</summary>
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
            await _travel.EnterAsync(player);
        }
        finally
        {
            if (this != null) isTransitioning = false;
        }
    }

    public void ActivateDoorCollider(int active) => _blockingCollider.enabled = active == 0;

    private async void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<PlayerActorIdentity>() == null || isTransitioning ||
            _travel?.IsTransitioning == true) return;
        try
        {
            await EnterAsync(collision.collider);
        }
        catch (Exception error)
        {
            if (this != null) Debug.LogException(error, this);
        }
    }
}
