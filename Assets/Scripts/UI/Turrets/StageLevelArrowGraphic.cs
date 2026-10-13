using UnityEngine;
using UnityEngine.UI;

/// <summary>기본 UI Image 두 개로 위·아래 화살표를 구성합니다.</summary>
[RequireComponent(typeof(RectTransform))]
public sealed class StageLevelArrowGraphic : MonoBehaviour
{
    private bool _pointsUp;
    private Image _left, _right;
    public bool PointsUp
    {
        get => _pointsUp;
        set
        {
            _pointsUp = value;
            if (_left == null) _left = Stroke("Left", -4f);
            if (_right == null) _right = Stroke("Right", 4f);
            _left.rectTransform.localRotation = Quaternion.Euler(0, 0, value ? 45f : -45f);
            _right.rectTransform.localRotation = Quaternion.Euler(0, 0, value ? -45f : 45f);
        }
    }

    private Image Stroke(string name, float x)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, 0);
        rect.sizeDelta = new Vector2(14, 4);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.4f, 1f, 1f);
        image.raycastTarget = false;
        return image;
    }
}
