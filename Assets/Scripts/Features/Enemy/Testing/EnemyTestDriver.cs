using UnityEngine;

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
        _controller.SetTargetAndDestination(_target);
    }

    private void Update()
    {
        if (_switchedToNextTarget || currentTarget != null)
            return;

        _switchedToNextTarget = true;
        _target = nextTarget;
        _controller.SetTargetAndDestination(_target);
    }

    private void FixedUpdate()
    {   _controller.ExecuteBehavior();
    }
}