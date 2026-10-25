using UnityEngine;

/// <summary>Plays an authored short sprite effect and removes its own instance.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerSpriteEffect : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float _duration = .2f;
    private void OnEnable() => Destroy(gameObject, Mathf.Max(.01f, _duration));

    public void SetTint(Color tint, SpriteRenderer source)
    {
        var renderer = GetComponent<SpriteRenderer>();
        renderer.color = Color.Lerp(Color.white, tint, .35f);
        if (source == null) return;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
    }
}
