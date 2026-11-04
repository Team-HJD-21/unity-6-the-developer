using UnityEngine;

namespace TeamHJD.Game.Enemy.Effects
{
    /// <summary>감속 상태에 따라 고리와 이동 잔상을 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneSlowEffect : MonoBehaviour
    {
        [Header("연출 위치")]
        [SerializeField] private Transform effectAnchor;

        [Header("프리팹")]
        [SerializeField] private GameObject ringPrefab;
        [SerializeField] private GameObject trailPrefab;

        [Header("이동 잔상")]
        [SerializeField] private bool emitTrail;
        [SerializeField, Min(0.01f)] private float trailInterval = 0.15f;
        [SerializeField, Min(0f)] private float minimumMoveSpeed = 0.05f;

        private bool _isSlowActive;
        private GameObject _ringInstance;
        private Vector3 _previousPosition;
        private float _nextTrailTime;

        private Vector3 EffectPosition =>
            effectAnchor != null ? effectAnchor.position : transform.position;

        /// <summary>외부에서 확정한 감속 상태를 연출에 반영한다. 지속시간은 호출자가 관리한다.</summary>
        public void SetSlowActive(bool active)
        {
            // 재피격으로 시간이 갱신돼도 고리를 중복 생성하지 않는다.
            if (_isSlowActive == active)
                return;

            _isSlowActive = active;

            if (active && isActiveAndEnabled)
                BeginVisuals();
            else
                ClearVisuals();
        }

        private void OnEnable()
        {
            if (_isSlowActive)
                BeginVisuals();
        }

        private void OnDisable()
        {
            ClearVisuals();
        }

        private void BeginVisuals()
        {
            _previousPosition = EffectPosition;
            _nextTrailTime = Time.time;

            if (_ringInstance == null && ringPrefab != null)
            {
                _ringInstance = Instantiate(
                    ringPrefab,
                    EffectPosition,
                    Quaternion.identity);
            }
        }

        private void LateUpdate()
        {
            if (!_isSlowActive)
                return;

            Vector3 position = EffectPosition;

            if (_ringInstance != null)
                _ringInstance.transform.position = position;

            // 깊이 정렬을 위한 Z축 변화는 이동으로 취급하지 않는다.
            Vector2 movement = (Vector2)(position - _previousPosition);
            _previousPosition = position;

            if (!emitTrail || trailPrefab == null || Time.deltaTime <= 0f)
                return;

            float minimumDistance = minimumMoveSpeed * Time.deltaTime;

            if (movement.sqrMagnitude <= minimumDistance * minimumDistance)
                return;

            if (Time.time < _nextTrailTime)
                return;

            // 월드에 남겨두므로 대상이 움직여도 잔상은 따라가지 않는다.
            Instantiate(trailPrefab, position, Quaternion.identity);
            _nextTrailTime = Time.time + trailInterval;
        }

        private void ClearVisuals()
        {
            if (_ringInstance != null)
            {
                Destroy(_ringInstance);
                _ringInstance = null;
            }
        }
    }
}
