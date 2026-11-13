using UnityEngine;
using UnityEngine.Serialization;

/// <summary>실제 사격 성공 시에만 총구의 발사 효과를 잠깐 표시합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(295)]
public sealed class PlayerMuzzleFlash : MonoBehaviour
{
    [FormerlySerializedAs("flashObject")]
    [SerializeField] private GameObject _flashVisual;
    [FormerlySerializedAs("flashDuration")]
    [SerializeField, Min(.01f)] private float _duration = .05f;
    [SerializeField] private Transform _muzzle;
    private float _hideAt;
    private bool _showing;
    private bool _followMuzzle;

    private void Awake() => Hide();
    private void OnEnable() => Hide();
    private void OnDisable() => Hide();

    public void Show()
    {
        _followMuzzle = false;
        if (!isActiveAndEnabled || _flashVisual == null || _flashVisual == gameObject ||
            !_flashVisual.transform.IsChildOf(transform)) return;
        if (_muzzle != null && _muzzle.IsChildOf(transform)) _flashVisual.transform.position = _muzzle.position;
        _flashVisual.SetActive(true);
        _showing = true;
        _hideAt = Time.time + Mathf.Max(.01f, _duration);
    }

    public void Show(Color tint)
    {
        if (_flashVisual != null && _flashVisual.TryGetComponent(out SpriteRenderer renderer))
            renderer.color = Color.Lerp(Color.white, tint, .35f);
        Show();
    }

    public void Show(Color tint, Quaternion rotation)
    {
        Show(tint);
        if (_showing)
        {
            _followMuzzle = true;
            _flashVisual.transform.rotation = rotation;
        }
    }

    private void Update() { if (_showing && Time.time >= _hideAt) Hide(); }
    private void LateUpdate() => RefreshMuzzlePose();

    public void RefreshMuzzlePose()
    {
        if (_showing && _followMuzzle && _flashVisual != null && _muzzle != null)
            _flashVisual.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
    }

    private void Hide()
    {
        _showing = false;
        _followMuzzle = false;
        if (_flashVisual != null && _flashVisual != gameObject && _flashVisual.transform.IsChildOf(transform))
            _flashVisual.SetActive(false);
    }
}
