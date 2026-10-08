using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class DoorOperation : MonoBehaviour
{
    private Animator _animator;
    private BoxCollider2D _blockingCollider;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _blockingCollider = GetComponent<BoxCollider2D>();
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        
        _animator.SetBool("nearPlayer", true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        _animator.SetBool("nearPlayer", false);
    }
    
    // 애니메이션 이벤트로 처리할 함수들
    public void ActivateDoorCollider(int active)
    {
        _blockingCollider.enabled = active != 0;
    }
    
}
