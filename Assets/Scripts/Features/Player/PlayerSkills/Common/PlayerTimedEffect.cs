using UnityEngine;
using UnityEngine.Serialization;

/// <summary>폭발 등 독립적인 일회성 효과를 일정 시간 후 정리합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerTimedEffect : MonoBehaviour
{
    [FormerlySerializedAs("effect")]
    [SerializeField] private GameObject _effectVisual;
    [SerializeField, Min(.01f)] private float _lifetime = .8f;
    private float _elapsed;

    private void OnEnable()
    {
        _elapsed = 0f;
        if (_effectVisual != null && _effectVisual != gameObject) _effectVisual.SetActive(true);
    }

    private void Update()
    {
        if (!PlayerFieldSimulationUtility.CanAdvance(this)) return;
        _elapsed += Time.deltaTime;
        if (_elapsed >= Mathf.Max(.01f, _lifetime)) Destroy(gameObject);
    }
}
