using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 결정된 몬스터 애니메이션 상태를 클라이언트에 동기화한다.
/// 이동 상태는 <see cref="NetworkVariable{T}"/>로, 공격은 RPC 이벤트로 전달한다.
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
        // 동기화 값이 변경되면 각 클라이언트의 Animator에 반영한다.
        _movementState.OnValueChanged += OnMovementStateChanged;

        // 서버는 최초 상태를 기록하고 클라이언트는 전달받은 상태를 즉시 적용한다.
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
        PlayAttackRpc();
    }

    /// <summary>
    /// 공격 이벤트를 로컬 Animator에 한 번만 적용한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayAttackRpc()
    {
        _animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Animator의 이동 값을 읽어 변경된 상태만 네트워크에 기록한다.
    /// </summary>
    private void PublishMovementState()
    {
        // 서버 Animator의 현재 이동 값을 네트워크 전송 구조체로 변환한다.
        var nextState = new PoCMonsterAnimatorState(
            _animator.GetBool(IsMovingHash),
            _animator.GetFloat(MoveXHash),
            _animator.GetFloat(MoveYHash),
            _animator.GetFloat(MoveAnimSpeedHash));

        // 실제 값이 변경된 경우에만 NetworkVariable을 갱신한다.
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
        // 서버는 원본 Animator를 사용하므로 수신한 상태는 클라이언트에만 적용한다.
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

    /// <summary>
    /// 동기화할 이동 애니메이션 상태를 생성한다.
    /// </summary>
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

    /// <summary>
    /// 전달된 객체가 동일한 이동 상태인지 확인한다.
    /// </summary>
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
