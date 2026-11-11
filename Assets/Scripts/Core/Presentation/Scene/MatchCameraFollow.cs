// 현재 로컬 Player actor의 XY 위치를 부드럽게 추적합니다.
// 임시 시점 선택/블렌딩은 CameraManager에 양보하고 기존 Bind/Snap 호출을 유지합니다.

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
        private CameraManager _manager;
        public Transform Target => _target;
        public CameraManager Manager => _manager;

        private void Awake()
        {
            _manager = GetComponent<CameraManager>() ?? gameObject.AddComponent<CameraManager>();
            _manager.Attach(this);
        }

        public void Bind(Transform target)
        {
            _target = target;
            _velocity = Vector3.zero;
            _manager.RebindPlayer();
            SnapToTarget();
        }

        /// <summary>생성·포털 순간이동 직후 호출합니다. 평소 추적은 유지하고 이동 구간의 카메라 비행만 제거합니다.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            _velocity = Vector3.zero;
            if (_manager.ControlsTransform) { _manager.SnapToCurrentView(); return; }
            transform.position = new Vector3(_target.position.x, _target.position.y, transform.position.z);
        }

        private void LateUpdate()
        {
            if (_target == null || _manager.ControlsTransform || CameraManager.Current != _manager) return;

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
