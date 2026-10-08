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

        public void Bind(Transform target)
        {
            _target = target;
            _velocity = Vector3.zero;
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
