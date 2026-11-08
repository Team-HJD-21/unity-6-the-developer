// Player 장치 효과의 기한과 종료를 주입된 필드 시계로 처리합니다.
using TeamHJD.Game.Presentation.Scene;
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
        if (!FieldSimulationTime.CanAdvance(this)) return;
        _elapsed += FieldSimulationTime.DeltaTime(this);
        if (_elapsed >= Mathf.Max(.01f, _lifetime)) Destroy(gameObject);
    }
}
