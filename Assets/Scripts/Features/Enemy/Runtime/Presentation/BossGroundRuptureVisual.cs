using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>예고가 끝난 월드 위치에서 가시 프레임을 재생한다. 피해와 충돌은 처리하지 않는다.</summary>
public sealed class BossGroundRuptureVisual : MonoBehaviour
{
    [Header("가시 연출")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField, Min(1f)] private float framesPerSecond = 24f;
    [Tooltip("스프라이트 원본에서 가장 넓은 가시 영역의 지름이다. 투명 여백은 제외한다.")]
    [SerializeField, Min(.01f)] private float spriteDiameter = 1.73f;

    private double[] _startedAt;
    private Vector3[] _centers;
    private float[] _scales;
    private int _next;

    public bool IsConfigured => frames != null && frames.Length > 0 && renderers != null && renderers.Length > 0;

    private void Awake()
    {
        int count = renderers != null ? renderers.Length : 0;
        _startedAt = new double[count];
        _centers = new Vector3[count];
        _scales = new float[count];
        Clear();
    }

    /// <summary>연속 원이 겹쳐도 이전 가시를 유지하도록 연결된 렌더러를 순환 사용한다.</summary>
    public void Play(Vector2 center, float radius)
    {
        if (!IsConfigured || _startedAt == null) return;
        int slot = _next++ % renderers.Length;
        if (renderers[slot] == null) return;
        _startedAt[slot] = FieldSimulationTime.Seconds(this);
        _centers[slot] = new Vector3(center.x, center.y, transform.position.z);
        _scales[slot] = radius * 2f / spriteDiameter;
        renderers[slot].sprite = frames[0];
        renderers[slot].enabled = true;
        Place(slot);
    }

    private void LateUpdate()
    {
        if (_startedAt == null || !FieldSimulationTime.CanAdvance(this)) return;
        double now = FieldSimulationTime.Seconds(this);
        for (int i = 0; i < renderers.Length; i++)
        {
            var visual = renderers[i];
            if (visual == null || !visual.enabled) continue;
            int frame = Mathf.Max(0, (int)((now - _startedAt[i]) * framesPerSecond));
            if (frame >= frames.Length) { visual.enabled = false; continue; }
            visual.sprite = frames[frame];
            Place(i);
        }
    }

    /// <summary>보스의 이동·확대와 분리해 예고한 위치와 지름을 유지한다.</summary>
    private void Place(int slot)
    {
        Transform visual = renderers[slot].transform;
        visual.SetPositionAndRotation(_centers[slot], Quaternion.identity);
        Vector3 parent = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
        visual.localScale = new Vector3(_scales[slot] / Mathf.Max(.0001f, Mathf.Abs(parent.x)),
            _scales[slot] / Mathf.Max(.0001f, Mathf.Abs(parent.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(parent.z)));
    }

    public void Clear()
    {
        if (renderers == null) return;
        foreach (var visual in renderers)
            if (visual != null) visual.enabled = false;
        _next = 0;
    }

    private void OnDisable() => Clear();
}
