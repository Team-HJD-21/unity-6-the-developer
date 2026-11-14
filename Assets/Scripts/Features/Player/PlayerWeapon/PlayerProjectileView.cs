using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Selects authored flight clips and plays the projectile's impact prefab.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class PlayerProjectileView : MonoBehaviour
{
    [SerializeField] private PlayerSpriteEffect _impactPrefab;
    private SpriteRenderer _renderer;
    private Color _tint;

    public void Configure(bool cryogenic, Color tint)
    {
        _renderer = GetComponent<SpriteRenderer>();
        _tint = tint;
        // Flight frames already contain an ivory core and a warm/cyan perimeter.
        _renderer.color = Color.white;
        GetComponent<Animator>().Play(cryogenic ? "Base Layer.Cryo" : "Base Layer.Warm", 0, 0f);
    }

    public void ShowImpact(Vector2? position = null)
    {
        if (_impactPrefab == null) return;
        var point = position.HasValue ? new Vector3(position.Value.x, position.Value.y, transform.position.z) : transform.position;
        var effect = Instantiate(_impactPrefab, point, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(effect.gameObject, gameObject.scene);
        effect.SetTint(_tint, _renderer);
    }
}
