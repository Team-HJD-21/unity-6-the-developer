// 실제 Player 체력을 Enemy가 읽는 타깃 상태로 연결합니다.
// 타깃 누락으로 분대가 정지하지 않도록 생성·체력 변경·비활성을 동기화합니다.
using UnityEngine;

namespace TeamHJD.Game.Enemies.Targeting
{
    /// <summary>
    /// 플레이어 상태를 TargetableComponent의 타깃 유효성에 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInfo), typeof(TargetableComponent))]
    public sealed class PlayerTargetableAdapter : MonoBehaviour
    {
        private PlayerInfo _player;
        private TargetableComponent _targetable;

        // Unity 생명주기

        /// <summary>플레이어 체력을 읽을 참조를 연결하고 타깃 유형을 지정한다.</summary>
        private void Awake()
        {
            _player = GetComponent<PlayerInfo>();
            _targetable = GetComponent<TargetableComponent>();
            _targetable.SetTargetType(TargetType.Player);
            Synchronize();
        }

        /// <summary>플레이어 체력 변경을 구독하고 현재 타깃 상태를 반영한다.</summary>
        private void OnEnable()
        {
            _player.onHpChange.AddListener(OnHealthChanged);
            Synchronize();
        }

        /// <summary>다른 컴포넌트 초기화가 끝난 뒤 플레이어 체력을 다시 읽는다.</summary>
        // 다른 컴포넌트의 Awake 순서는 보장되지 않으므로 첫 행동 전에 다시 읽습니다.
        private void Start() => Synchronize();

        /// <summary>체력 이벤트 구독을 해제하고 타깃 후보에서 제외한다.</summary>
        private void OnDisable()
        {
            if (_player != null) _player.onHpChange.RemoveListener(OnHealthChanged);
            if (_targetable != null) _targetable.enabled = false;
        }

        // 내부 처리

        /// <summary>체력 변경 이벤트를 받으면 원본 플레이어 상태를 다시 동기화한다.</summary>
        private void OnHealthChanged(int currentHealth, int maxHealth) => Synchronize();

        /// <summary>플레이어의 현재 체력과 활성 상태를 Enemy 타깃 정보에 반영한다.</summary>
        private void Synchronize()
        {
            if (_player == null || _targetable == null) return;
            _targetable.SetHealth(_player.curHp, _player.maxHp);
            _targetable.enabled = isActiveAndEnabled && _player.curHp > 0;
        }
    }
}
