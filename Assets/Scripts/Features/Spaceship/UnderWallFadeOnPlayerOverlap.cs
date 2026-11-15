using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnderWallFadeOnPlayerOverlap : MonoBehaviour
{
    private const float MinimumAlpha = 0.3f;
    private readonly HashSet<Collider2D> _playerColliders = new();
    private Tilemap _tilemap;
    private BoxCollider2D _fadeTrigger;
    private Transform _player;

    private void Awake()
    {
        _tilemap = GetComponentInParent<Tilemap>();
        _fadeTrigger = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!_playerColliders.Contains(other))
        {
            var identity = other.GetComponentInParent<PlayerActorIdentity>();
            if (identity == null) return;
            _playerColliders.Add(other);
            _player = identity.transform;
        }
        UpdateAlpha();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!_playerColliders.Remove(other) || _playerColliders.Count > 0) return;
        _player = null;
        UpdateAlpha();
    }

    private void UpdateAlpha()
    {
        if (_tilemap == null || _fadeTrigger == null) return;

        Bounds bounds = _fadeTrigger.bounds;
        float proximity = _player == null ? 0f : 1f - Mathf.InverseLerp(bounds.min.y, bounds.max.y, _player.position.y);
        float smoothProximity = proximity * proximity * (3f - 2f * proximity);
        Color color = _tilemap.color;
        color.a = Mathf.Lerp(1f, MinimumAlpha, smoothProximity);
        _tilemap.color = color;
    }

    private void OnDisable()
    {
        _playerColliders.Clear();
        _player = null;
        if (_tilemap == null) return;
        Color color = _tilemap.color;
        color.a = 1f;
        _tilemap.color = color;
    }
}
