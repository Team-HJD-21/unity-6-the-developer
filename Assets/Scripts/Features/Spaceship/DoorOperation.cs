using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class DoorOperation : MonoBehaviour
{
    private readonly HashSet<Collider2D> _nearPlayers = new();
    private Animator _animator;
    private BoxCollider2D _blockingCollider;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _blockingCollider = GetComponent<BoxCollider2D>();
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActiveAndEnabled || other.isTrigger || other.gameObject.layer != LayerMask.NameToLayer("PlayerFeetBox")) return;
        if (_nearPlayers.Add(other) && _nearPlayers.Count == 1) _animator.SetBool("nearPlayer", true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_nearPlayers.Remove(other) && _nearPlayers.Count == 0) _animator.SetBool("nearPlayer", false);
    }

    private void OnDisable()
    {
        _nearPlayers.Clear();
        if (_animator != null && _animator.isActiveAndEnabled) _animator.SetBool("nearPlayer", false);
    }
    
    // 애니메이션 이벤트로 처리할 함수들
    public void ActivateDoorCollider(int active)
    {
        _blockingCollider.enabled = active != 0;
    }
    
}
