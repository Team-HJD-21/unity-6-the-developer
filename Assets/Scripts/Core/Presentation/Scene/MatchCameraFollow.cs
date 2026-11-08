// Match Scene에서 카메라가 현재 Player actor를 부드럽게 따라가도록 합니다.

using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MatchCameraFollow : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        private Transform _target;
        private Vector3 _velocity;
        public Transform Target => _target;

        public void Bind(Transform target)
        {
            _target = target;
            _velocity = Vector3.zero;
            SnapToTarget();
        }

        /// <summary>생성·포털 순간이동 직후 호출합니다. 평소 추적은 유지하고 이동 구간의 카메라 비행만 제거합니다.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            _velocity = Vector3.zero;
            transform.position = new Vector3(_target.position.x, _target.position.y, transform.position.z);
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPosition = new Vector3(
                _target.position.x,
                _target.position.y,
                transform.position.z);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref _velocity,
                smoothTime);
        }
    }
}
