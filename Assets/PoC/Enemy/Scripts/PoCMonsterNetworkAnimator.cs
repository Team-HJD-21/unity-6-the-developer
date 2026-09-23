using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 몬스터 애니메이션을 네트워크로 동기화한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PoCMonsterNetworkAnimator : NetworkBehaviour
{
    // Animator 파라미터
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    // 네트워크 상태
    private readonly NetworkVariable<PoCMonsterAnimatorState> _movementState = new();
    private Animator _animator;
    private uint _nextAttackSequence;
    private uint _lastAppliedAttackSequence;

    /// <summary>
    /// Animator 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 이동 상태 변경 이벤트를 등록하고 최초 상태를 적용한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _movementState.OnValueChanged += OnMovementStateChanged;

        if (IsServer)
            PublishMovementState();
        else
            ApplyMovementState(_movementState.Value);
    }

    /// <summary>
    /// 이동 상태 변경 이벤트를 해제한다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _movementState.OnValueChanged -= OnMovementStateChanged;
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 서버 Animator의 최신 이동 상태를 프레임 마지막에 전송한다.
    /// </summary>
    private void LateUpdate()
    {
        if (IsSpawned && IsServer)
            PublishMovementState();
    }

    /// <summary>
    /// 공격을 모든 클라이언트에 전달한다.
    /// </summary>
    public void PlayAttack()
    {
        if (!IsSpawned || !IsServer)
            return;

        _nextAttackSequence++;
        PlayAttackRpc(_nextAttackSequence);
    }

    /// <summary>
    /// 공격 이벤트를 로컬 Animator에 한 번만 적용한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayAttackRpc(uint sequence)
    {
        // 같은 공격 중복 실행 방지
        if (sequence == _lastAppliedAttackSequence)
            return;

        _lastAppliedAttackSequence = sequence;
        _animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Animator의 이동 값을 읽어 변경된 상태만 네트워크에 기록한다.
    /// </summary>
    private void PublishMovementState()
    {
        var nextState = new PoCMonsterAnimatorState(
            _animator.GetBool(IsMovingHash),
            _animator.GetFloat(MoveXHash),
            _animator.GetFloat(MoveYHash),
            _animator.GetFloat(MoveAnimSpeedHash));

        if (!_movementState.Value.Equals(nextState))
            _movementState.Value = nextState;
    }

    /// <summary>
    /// 클라이언트가 새 이동 상태를 받으면 Animator에 적용한다.
    /// </summary>
    /// <param name="previous">변경 전 이동 상태.</param>
    /// <param name="current">변경 후 이동 상태.</param>
    private void OnMovementStateChanged(
        PoCMonsterAnimatorState previous,
        PoCMonsterAnimatorState current)
    {
        if (!IsServer)
            ApplyMovementState(current);
    }

    /// <summary>
    /// 동기화된 이동 상태를 Animator 파라미터에 적용한다.
    /// </summary>
    private void ApplyMovementState(PoCMonsterAnimatorState state)
    {
        _animator.SetBool(IsMovingHash, state.IsMoving);
        _animator.SetFloat(MoveXHash, state.MoveX);
        _animator.SetFloat(MoveYHash, state.MoveY);
        _animator.SetFloat(MoveAnimSpeedHash, state.MoveAnimSpeed);
    }
}

/// <summary>
/// 네트워크로 전달할 이동 애니메이션 값을 보관한다.
/// </summary>
public struct PoCMonsterAnimatorState : INetworkSerializable, IEquatable<PoCMonsterAnimatorState>
{
    // 이동 애니메이션 값
    public bool IsMoving;
    public float MoveX;
    public float MoveY;
    public float MoveAnimSpeed;

    public PoCMonsterAnimatorState(bool isMoving, float moveX, float moveY, float moveAnimSpeed)
    {
        IsMoving = isMoving;
        MoveX = moveX;
        MoveY = moveY;
        MoveAnimSpeed = moveAnimSpeed;
    }

    /// <summary>
    /// 이동 상태를 네트워크 버퍼에 쓰거나 버퍼에서 읽는다.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 송신과 수신에서 같은 순서로 값을 쓰고 읽는다.
        serializer.SerializeValue(ref IsMoving);
        serializer.SerializeValue(ref MoveX);
        serializer.SerializeValue(ref MoveY);
        serializer.SerializeValue(ref MoveAnimSpeed);
    }

    /// <summary>
    /// 두 이동 상태의 모든 값이 같은지 비교한다.
    /// </summary>
    public bool Equals(PoCMonsterAnimatorState other)
    {
        return IsMoving == other.IsMoving &&
               MoveX.Equals(other.MoveX) &&
               MoveY.Equals(other.MoveY) &&
               MoveAnimSpeed.Equals(other.MoveAnimSpeed);
    }

    public override bool Equals(object obj)
    {
        return obj is PoCMonsterAnimatorState other && Equals(other);
    }

    /// <summary>
    /// 이동 상태의 네 값을 조합한 해시를 반환한다.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(IsMoving, MoveX, MoveY, MoveAnimSpeed);
    }
}
