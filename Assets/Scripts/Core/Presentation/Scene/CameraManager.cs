// 로컬 화면의 출력 Camera와 임시 시점의 선택·전환·복귀를 관리합니다.
// 컷씬은 같은 출력 Camera를 움직이므로 Weather/입력의 카메라 참조를 교체하지 않습니다.
// Scene/actor 해제 시 임시 시점을 비우며 네트워크 권위나 Player 전투를 소유하지 않습니다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(1000)]
    public sealed class CameraManager : MonoBehaviour
    {
        private sealed class View : IDisposable
        {
            public readonly Transform Target;
            public float Size;
            public Rect? Viewport;
            public readonly float BlendSeconds;
            private CameraManager _owner;
            public View(CameraManager owner, Transform target, float size, float blendSeconds, Rect? viewport)
            { _owner = owner; Target = target; Size = size; BlendSeconds = blendSeconds; Viewport = viewport; }
            public void Dispose()
            {
                CameraManager owner = _owner; _owner = null;
                if (owner != null) owner.Remove(this);
            }
        }

        private static readonly List<CameraManager> Outputs = new List<CameraManager>();
        private readonly List<View> _views = new List<View>();
        private MatchCameraFollow _follow;
        private Camera _camera;
        private AudioListener _listener;
        private Vector3 _fromPosition;
        private Quaternion _fromRotation, _defaultRotation;
        private float _fromSize, _defaultSize, _defaultZ, _blendSeconds, _blendElapsed;
        private bool _blending;
        private Rect _defaultViewport, _fromViewport;
        private bool _ownsViewportAspect;

        public static CameraManager Current { get; private set; }
        /// <summary>로컬 출력 자체가 바뀔 때만 알립니다. 외부 구독자는 자신의 Scene 수명에서 해제합니다.</summary>
        public static event Action<Camera> OutputChanged;
        public Camera OutputCamera => _camera;
        public Transform ActiveTarget => _views.Count > 0 ? _views[_views.Count - 1].Target : _follow?.Target;
        public bool HasOverride => _views.Count > 0;
        public bool IsBlending => _blending;
        public event Action<Transform> ViewChanged;
        internal bool ControlsTransform => HasOverride || _blending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        { Outputs.Clear(); Current = null; OutputChanged = null; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _listener = GetComponent<AudioListener>();
            _defaultSize = _camera.orthographicSize;
            _defaultZ = transform.position.z;
            _defaultRotation = transform.rotation;
            _defaultViewport = _camera.rect;
        }

        internal void Attach(MatchCameraFollow follow) => _follow = follow;

        /// <summary>기존 Follow.Bind가 actor 교체/해제 때 호출합니다. 과거 컷씬 lease는 새 actor에 적용되지 않습니다.</summary>
        internal void RebindPlayer()
        {
            _views.Clear(); _blending = false;
            _camera.orthographicSize = _defaultSize;
            _camera.rect = _defaultViewport;
            if (_ownsViewportAspect) { _camera.ResetAspect(); _ownsViewportAspect = false; }
            transform.rotation = _defaultRotation;
            if (isActiveAndEnabled && _follow != null && _follow.Target != null) Register();
            NotifyObservers(ViewChanged, ActiveTarget);
        }

        /// <summary>로컬 컷씬/연출 소유자가 호출합니다. 반환 lease를 해제하면 직전 시점으로 돌아갑니다.</summary>
        /// <remarks>XY 위치·회전·선택적 확대를 같은 출력에 적용합니다. 실제 Camera나 AudioListener를 만들지 않습니다.</remarks>
        public IDisposable PushView(Transform target, float blendSeconds = .25f, float? orthographicSize = null, Rect? viewport = null)
        {
            if (target == null || !target.gameObject.scene.IsValid() || !target.gameObject.scene.isLoaded)
                throw new ArgumentException("Use a live Scene view target.", nameof(target));
            if (!isActiveAndEnabled || Current != this || _follow?.Target == null)
                throw new InvalidOperationException("Use the current local player camera.");
            float size = orthographicSize ?? _defaultSize;
            if (!float.IsFinite(blendSeconds) || blendSeconds < 0 || !float.IsFinite(size) || size <= 0)
                throw new ArgumentOutOfRangeException(nameof(blendSeconds), "Use finite blend seconds and a positive view size.");
            if (viewport.HasValue && !ValidViewport(viewport.Value)) throw new ArgumentOutOfRangeException(nameof(viewport));
            var view = new View(this, target, size, blendSeconds, viewport);
            _views.Add(view); BeginBlend(blendSeconds);
            NotifyObservers(ViewChanged, target);
            return view;
        }

        /// <summary>자신이 소유한 현재 시점의 배율만 갱신합니다. 카메라/리스너나 view lease를 다시 만들지 않습니다.</summary>
        public bool TrySetViewSize(Transform target, float orthographicSize)
        {
            if (target == null || !float.IsFinite(orthographicSize) || orthographicSize <= 0 ||
                _views.Count == 0 || _views[_views.Count - 1].Target != target) return false;
            _views[_views.Count - 1].Size = orthographicSize;
            return true;
        }
        /// <summary>현재 view 소유자만 임시 화면 영역을 바꿉니다. lease 종료 시 기본 viewport로 복귀합니다.</summary>
        public bool TrySetViewViewport(Transform target, Rect viewport)
        {
            if (target == null || !ValidViewport(viewport) || _views.Count == 0 || _views[_views.Count-1].Target != target) return false;
            _views[_views.Count-1].Viewport = viewport; return true;
        }
        private static bool ValidViewport(Rect r) => float.IsFinite(r.x) && float.IsFinite(r.y) && float.IsFinite(r.width) &&
            float.IsFinite(r.height) && r.width > 0 && r.height > 0 && r.xMin >= 0 && r.yMin >= 0 && r.xMax <= 1 && r.yMax <= 1;

        /// <summary>포털 이동 직후 기존 Follow가 호출합니다. 현재 선택된 시점으로 즉시 맞추고 이동 비행을 없앱니다.</summary>
        internal void SnapToCurrentView()
        {
            _blending = false;
            ApplyView(1);
        }

        private void Remove(View view)
        {
            int index = _views.IndexOf(view);
            if (index < 0) return; // 이전 actor의 이미 끝난 lease는 새 시점을 바꾸지 않습니다.
            bool selected = index == _views.Count - 1;
            _views.RemoveAt(index);
            if (!selected) return;
            PruneDestroyedViews();
            BeginBlend(view.BlendSeconds);
            NotifyObservers(ViewChanged, ActiveTarget);
        }

        private bool PruneDestroyedViews()
        {
            bool changed = false;
            for (int index = _views.Count - 1; index >= 0; index--)
                if (_views[index].Target == null) { _views.RemoveAt(index); changed = true; }
            return changed;
        }

        private void BeginBlend(float seconds)
        {
            _fromPosition = transform.position; _fromRotation = transform.rotation;
            _fromSize = _camera.orthographicSize;
            _fromViewport = _camera.rect;
            _blendSeconds = seconds; _blendElapsed = 0; _blending = seconds > 0;
            if (!_blending) ApplyView(1);
        }

        private void LateUpdate()
        {
            if (Current != this) return;
            if (_follow == null || _follow.Target == null)
            {
                if (ControlsTransform) RebindPlayer();
                return;
            }
            if (PruneDestroyedViews()) { BeginBlend(0); NotifyObservers(ViewChanged, ActiveTarget); }
            if (!ControlsTransform) return;
            // 게임 정지 중에도 로컬 시점 전환은 완료됩니다. Match 시계나 게임 배속을 수정하지 않습니다.
            _blendElapsed += Time.unscaledDeltaTime;
            float amount = _blending ? Mathf.Clamp01(_blendElapsed / _blendSeconds) : 1;
            ApplyView(Mathf.SmoothStep(0, 1, amount));
            if (amount >= 1) _blending = false;
        }

        private void ApplyView(float amount)
        {
            Transform target = ActiveTarget;
            if (target == null) return;
            bool temporary = _views.Count > 0;
            var destination = new Vector3(target.position.x, target.position.y, _defaultZ);
            Quaternion rotation = temporary ? target.rotation : _defaultRotation;
            float size = temporary ? _views[_views.Count - 1].Size : _defaultSize;
            Rect viewport = temporary ? _views[_views.Count - 1].Viewport ?? _defaultViewport : _defaultViewport;
            transform.SetPositionAndRotation(Vector3.Lerp(_fromPosition, destination, amount),
                Quaternion.Slerp(_fromRotation, rotation, amount));
            _camera.orthographicSize = Mathf.Lerp(_fromSize, size, amount);
            _camera.rect = new Rect(Vector2.Lerp(_fromViewport.position,viewport.position,amount),Vector2.Lerp(_fromViewport.size,viewport.size,amount));
            // Camera.rect 만 바꿔도 aspect는 기본 screen 비율에 남으므로 임시 영역의 실제 픽셀 비율을 지정합니다.
            if (temporary && _views[_views.Count-1].Viewport.HasValue)
            { var pixels=_camera.pixelRect; _camera.aspect = pixels.width / Mathf.Max(.001f,pixels.height); _ownsViewportAspect = true; }
            else if (_ownsViewportAspect) { _camera.ResetAspect(); _ownsViewportAspect = false; }
        }

        private void Register()
        {
            Outputs.Remove(this); Outputs.Add(this);
            SelectOutput(this);
        }

        private static void SelectOutput(CameraManager next)
        {
            foreach (CameraManager output in Outputs)
                if (output != null) output.SetOutputEnabled(output == next);
            if (Current == next) return;
            Current = next;
            NotifyObservers(OutputChanged, next != null ? next.OutputCamera : null);
        }

        private static void NotifyObservers<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            // 표현 구독자의 실패 때문에 반환 lease나 다른 Scene의 복귀 처리가 유실되지 않게 합니다.
            foreach (Action<T> observer in handlers.GetInvocationList())
                try { observer(value); } catch (Exception error) { Debug.LogException(error); }
        }

        private void SetOutputEnabled(bool value)
        {
            if (_camera != null) _camera.enabled = value;
            if (_listener != null) _listener.enabled = value;
        }

        private void OnEnable()
        {
            if (_follow != null && _follow.Target != null) Register();
        }

        private void OnDisable()
        {
            _views.Clear(); _blending = false;
            if (_camera != null)
            {
                _camera.orthographicSize = _defaultSize;
                _camera.rect = _defaultViewport;
                if (_ownsViewportAspect) { _camera.ResetAspect(); _ownsViewportAspect = false; }
                transform.rotation = _defaultRotation;
            }
            SetOutputEnabled(false);
            if (!Outputs.Remove(this)) return;
            Outputs.RemoveAll(output => output == null);
            SelectOutput(Outputs.Count > 0 ? Outputs[Outputs.Count - 1] : null);
        }
    }
}
