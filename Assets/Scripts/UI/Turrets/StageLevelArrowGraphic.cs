using UnityEngine;

/// <summary>프리팹에 배치된 두 선의 방향만 갱신하는 화살표 View입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class StageLevelArrowGraphic : MonoBehaviour
{
    [SerializeField] private RectTransform leftStroke;
    [SerializeField] private RectTransform rightStroke;
    [SerializeField] private bool pointsUp;

    private void Awake() => RefreshDirection();
    private void OnValidate() => RefreshDirection();

    private void RefreshDirection()
    {
        float angle = pointsUp ? 45f : -45f;
        if (leftStroke != null) leftStroke.localRotation = Quaternion.Euler(0, 0, angle);
        if (rightStroke != null) rightStroke.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}
