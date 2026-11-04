using UnityEngine;

/// <summary>
/// 대시 중 이동 거리와 표시 간격에 맞춰 미리 배치한 잔상을 순환 사용합니다.
/// 잔상 한 개의 모습과 페이드는 PlayerDashAfterimageEffect가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(400)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerDashTrail")]
public sealed class PlayerDashAfterimageController : MonoBehaviour
{
    [SerializeField] private PlayerDash _dash;
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private SpriteRenderer _weapon;
    [SerializeField] private PlayerDashAfterimageEffect[] _images;
    [SerializeField, Min(.01f)] private float _interval = .045f;
    [SerializeField, Min(0)] private float _minimumDistance = .15f;
    private Vector3 _lastPosition;
    private float _nextEmission;
    private int _nextIndex;
    private bool _wasDashing;
    public bool IsConfigured => _dash != null && _body != null && _weapon != null &&
        _images != null && _images.Length > 0 && System.Array.TrueForAll(_images, image => image != null && image.IsConfigured);

    private void Awake()
    {
        if (!IsConfigured) Debug.LogError("[PlayerDashAfterimageController] Connect the dash, body, weapon and authored afterimage slots.", this);
    }

    private void LateUpdate()
    {
        if (!IsConfigured) return;
        if (!_dash.CanUseInput) { Clear(); return; }
        if (!_dash.IsDashing) { _wasDashing = false; return; }
        if (!_wasDashing)
        {
            _wasDashing = true;
            _lastPosition = transform.position;
            _nextEmission = Time.time;
        }
        if (Time.time < _nextEmission ||
            ((Vector2)(transform.position - _lastPosition)).sqrMagnitude < _minimumDistance * _minimumDistance) return;
        _images[_nextIndex].Capture(_body, _weapon);
        _nextIndex = (_nextIndex + 1) % _images.Length;
        _nextEmission = Time.time + Mathf.Max(.01f, _interval);
        _lastPosition = transform.position;
    }

    private void Clear()
    {
        _wasDashing = false;
        if (_images == null) return;
        foreach (var image in _images) if (image != null) image.Clear();
    }

    private void OnDisable() => Clear();
}
