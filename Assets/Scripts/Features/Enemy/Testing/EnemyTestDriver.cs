using UnityEngine;

/// <summary>
/// 네트워크 없는 테스트 씬에서 타깃 배정과 행동 실행을 담당한다.
/// EnemyNetworkController와 동시에 행동을 호출하지 않도록 구성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyTestDriver : MonoBehaviour
{
    [SerializeField] private Transform currentTarget;
    [SerializeField] private Transform nextTarget;
    private Transform _target;

    private EnemyController  _controller;
    private bool _switchedToNextTarget;

    private void Awake()
    {
        _controller = GetComponent<EnemyController>();
    }
    
    private void Start()
    {
        _target = currentTarget;
        _controller.AssignTarget(_target);
    }

    private void Update()
    {
        if (_switchedToNextTarget || currentTarget != null)
            return;

        // 첫 타깃이 제거되면 다음 타깃으로 한 번만 전환한다.
        _switchedToNextTarget = true;
        _target = nextTarget;
        _controller.AssignTarget(_target);
    }

    private void FixedUpdate()
    {   _controller.ExecuteBehavior();
    }
}
