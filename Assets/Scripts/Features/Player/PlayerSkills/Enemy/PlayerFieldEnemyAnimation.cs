using System.Collections.Generic;
using UnityEngine;

/// <summary>장비로 유도된 적의 표현만 갱신합니다. 전송은 기존 네트워크 Animator가 담당합니다.</summary>
public sealed class PlayerFieldEnemyAnimation
{
    private readonly Animator _animator;
    private readonly EnemyNetworkAnimator _networkAnimator;
    private readonly Dictionary<int, AnimatorControllerParameterType> _parameters = new();
    public PlayerFieldEnemyAnimation(GameObject actor)
    {
        _animator = actor.GetComponent<Animator>();
        _networkAnimator = actor.GetComponent<EnemyNetworkAnimator>();
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        foreach (var parameter in _animator.parameters) _parameters[parameter.nameHash] = parameter.type;
    }
    public void SetMovement(Vector2 direction, bool moving)
    {
        SetBool("IsMoving", moving);
        SetFloat("MoveX", Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? Mathf.Sign(direction.x) : 0);
        SetFloat("MoveY", Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? 0 : Mathf.Sign(direction.y));
        SetFloat("MoveAnimSpeed", 1);
    }
    public void PlayAttack()
    {
        if (_networkAnimator != null && _networkAnimator.IsSpawned && _networkAnimator.IsServer)
            _networkAnimator.PlayAttack();
        else if (HasParameter("Attack", AnimatorControllerParameterType.Trigger)) _animator.SetTrigger("Attack");
    }
    private bool HasParameter(string name, AnimatorControllerParameterType type) =>
        _animator != null && _parameters.TryGetValue(Animator.StringToHash(name), out var actual) && actual == type;
    private void SetBool(string name, bool value) { if (HasParameter(name, AnimatorControllerParameterType.Bool)) _animator.SetBool(name, value); }
    private void SetFloat(string name, float value) { if (HasParameter(name, AnimatorControllerParameterType.Float)) _animator.SetFloat(name, value); }
}
