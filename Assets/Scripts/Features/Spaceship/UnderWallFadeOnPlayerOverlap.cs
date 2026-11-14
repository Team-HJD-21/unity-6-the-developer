using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnderWallFadeOnPlayerOverlap : MonoBehaviour
{
    private const float FadeDuration = 1f;
    private const float OverlapAlpha = 0.6f;
    private readonly HashSet<Collider2D> _playerColliders = new();
    private Tilemap _tilemap;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _tilemap = GetComponentInParent<Tilemap>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerActorIdentity>() == null || !_playerColliders.Add(other)) return;
        FadeTo(OverlapAlpha);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!_playerColliders.Remove(other) || _playerColliders.Count > 0) return;
        FadeTo(1f);
    }

    private void FadeTo(float targetAlpha)
    {
        if (_tilemap == null) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAlpha(targetAlpha));
    }

    private IEnumerator FadeAlpha(float targetAlpha)
    {
        float startAlpha = _tilemap.color.a;
        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FadeDuration);
            float smoothT = t * t * (3f - 2f * t);
            Color color = _tilemap.color;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
            _tilemap.color = color;
            yield return null;
        }

        Color finalColor = _tilemap.color;
        finalColor.a = targetAlpha;
        _tilemap.color = finalColor;
        _fadeCoroutine = null;
    }

    private void OnDisable()
    {
        _playerColliders.Clear();
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
        if (_tilemap == null) return;
        Color color = _tilemap.color;
        color.a = 1f;
        _tilemap.color = color;
    }
}
