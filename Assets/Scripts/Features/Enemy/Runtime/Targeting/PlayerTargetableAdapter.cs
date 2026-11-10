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

        private void Awake()
        {
            _player = GetComponent<PlayerInfo>();
            _targetable = GetComponent<TargetableComponent>();
            _targetable.SetTargetType(TargetType.Player);
            Synchronize();
        }

        private void OnEnable()
        {
            _player.onHpChange.AddListener(OnHealthChanged);
            Synchronize();
        }

        // 다른 컴포넌트의 Awake 순서는 보장되지 않으므로 첫 행동 전에 다시 읽습니다.
        private void Start() => Synchronize();

        private void OnDisable()
        {
            if (_player != null) _player.onHpChange.RemoveListener(OnHealthChanged);
            if (_targetable != null) _targetable.enabled = false;
        }

        private void OnHealthChanged(int currentHealth, int maxHealth) => Synchronize();

        private void Synchronize()
        {
            if (_player == null || _targetable == null) return;
            _targetable.SetHealth(_player.curHp, _player.maxHp);
            _targetable.enabled = isActiveAndEnabled && _player.curHp > 0;
        }
    }
}
