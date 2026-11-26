using System;
using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Map", sourceAssembly: "Assembly-CSharp", sourceClassName: "StagePlayerOcclusion")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class FieldPlayerOcclusion : MonoBehaviour
    {
        private const float FadeOutSeconds = .18f;
        private const float RestoreSeconds = .24f;

        private static readonly HashSet<string> RockSprites = new(StringComparer.Ordinal)
        {
            "Overworld_Objects_22", "Overworld_Objects_23", "Overworld_Objects_24", "Overworld_Objects_25",
            "Overworld_Objects_26", "Overworld_Objects_27", "Overworld_Objects_28", "Overworld_Objects_29",
            "Overworld_Objects_30", "Overworld_Objects_31", "Overworld_Objects_38", "Overworld_Objects_39",
            "Overworld_Objects_40", "Overworld_Objects_46", "Overworld_Objects_49", "Overworld_Objects_50",
            "Overworld_Objects_51"
        };

        [SerializeField] private MatchCameraFollow _cameraFollow;
        [SerializeField, Range(0f, 1f)] private float _occludedAlpha = .23f;

        [SerializeField] private Sprite[] _additionalOccluders = Array.Empty<Sprite>();

        private readonly HashSet<Sprite> _additionalOccluderSprites = new();
        private Tilemap _tilemap;
        private Transform _player;
        private SpriteRenderer _body;
        private float _tileReach;
        private readonly List<FadingTile> _fadingTiles = new();
        private readonly HashSet<Vector3Int> _occluding = new();

        private struct FadingTile
        {
            public Vector3Int Cell;
            public TileBase Asset;
            public Color OriginalColor;
            public float Fade;
        }

        private void Awake()
        {
            _additionalOccluderSprites.UnionWith(_additionalOccluders);
            _tilemap = GetComponent<Tilemap>();
            var sprites = new Sprite[_tilemap.GetUsedSpritesCount()];
            int count = _tilemap.GetUsedSpritesNonAlloc(sprites);
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = sprites[i];
                if (IsOccluder(sprite))
                    _tileReach = Mathf.Max(_tileReach, sprite.bounds.center.magnitude + sprite.bounds.extents.magnitude);
            }
            _tileReach *= Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        }

        private void LateUpdate()
        {
            Transform player = _cameraFollow != null ? _cameraFollow.Target : null;
            if (_player != player)
            {
                _player = player;
                _body = player != null ? player.GetComponent<SpriteRenderer>() : null;
            }

            if (_body == null || !_body.enabled || !_body.gameObject.activeInHierarchy || _body.sprite == null ||
                _body.gameObject.scene != gameObject.scene)
            {
                RestoreColors();
                return;
            }

            Bounds bodyBounds = _body.bounds;
            Vector2 center = bodyBounds.center;
            Vector2 bodyHalfSize = Vector2.Min(bodyBounds.extents, new Vector2(.65f, .85f));

            CollectOccludingTiles(center, bodyHalfSize, _player.position.y);
            UpdateFades(Time.deltaTime);
        }

        private void CollectOccludingTiles(Vector2 center, Vector2 bodyHalfSize, float playerSortY)
        {
            _occluding.Clear();
            Vector2 search = bodyHalfSize + Vector2.one * (_tileReach + 1f);
            Vector3Int min = _tilemap.WorldToCell(center - search);
            Vector3Int max = _tilemap.WorldToCell(center + search);

            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (_tilemap.GetCellCenterWorld(cell).y >= playerSortY) continue;

                Sprite sprite = _tilemap.GetSprite(cell);
                if (!IsOccluder(sprite) || !OverlapsBody(cell, sprite, center, bodyHalfSize)) continue;

                _occluding.Add(cell);
            }
        }

        private void UpdateFades(float deltaTime)
        {
            for (int i = _fadingTiles.Count - 1; i >= 0; i--)
            {
                FadingTile tile = _fadingTiles[i];
                if (_tilemap.GetTile(tile.Cell) != tile.Asset)
                {
                    _fadingTiles.RemoveAt(i);
                    continue;
                }

                bool occluding = _occluding.Remove(tile.Cell);
                AdvanceFade(ref tile, occluding, deltaTime);
                if (!occluding && tile.Fade == 0f)
                    _fadingTiles.RemoveAt(i);
                else
                    _fadingTiles[i] = tile;
            }

            foreach (Vector3Int cell in _occluding)
            {
                var tile = new FadingTile
                {
                    Cell = cell,
                    Asset = _tilemap.GetTile(cell),
                    OriginalColor = _tilemap.GetColor(cell)
                };
                AdvanceFade(ref tile, true, deltaTime);
                _fadingTiles.Add(tile);
            }
        }

        private void AdvanceFade(ref FadingTile tile, bool occluding, float deltaTime)
        {
            float duration = occluding ? FadeOutSeconds : RestoreSeconds;
            float fade = Mathf.MoveTowards(tile.Fade, occluding ? 1f : 0f, deltaTime / duration);
            if (fade == tile.Fade) return;

            tile.Fade = fade;
            Color color = tile.OriginalColor;
            color.a *= Mathf.Lerp(1f, _occludedAlpha, Mathf.SmoothStep(0f, 1f, fade));
            SetTileColor(tile.Cell, color);
        }

        private bool OverlapsBody(Vector3Int cell, Sprite sprite, Vector2 center, Vector2 bodyHalfSize)
        {
            Matrix4x4 tile = transform.localToWorldMatrix *
                Matrix4x4.Translate(_tilemap.GetCellCenterLocal(cell)) *
                _tilemap.orientationMatrix * _tilemap.GetTransformMatrix(cell);
            Bounds bounds = sprite.bounds;
            Vector3 worldCenter = tile.MultiplyPoint3x4(bounds.center);
            Vector3 right = tile.MultiplyVector(Vector3.right * bounds.extents.x);
            Vector3 up = tile.MultiplyVector(Vector3.up * bounds.extents.y);

            return Mathf.Abs(worldCenter.x - center.x) < bodyHalfSize.x + Mathf.Abs(right.x) + Mathf.Abs(up.x) &&
                Mathf.Abs(worldCenter.y - center.y) < bodyHalfSize.y + Mathf.Abs(right.y) + Mathf.Abs(up.y);
        }

        public static bool IsDefaultOccluder(Sprite sprite)
            => sprite != null && (sprite.name.StartsWith("Overworld_Trees_", StringComparison.Ordinal) ||
                RockSprites.Contains(sprite.name));

        private bool IsOccluder(Sprite sprite)
            => IsDefaultOccluder(sprite) || (sprite != null && _additionalOccluderSprites.Contains(sprite));

        private void SetTileColor(Vector3Int cell, Color color)
        {
            TileFlags flags = _tilemap.GetTileFlags(cell);
            _tilemap.RemoveTileFlags(cell, TileFlags.LockColor);
            _tilemap.SetColor(cell, color);
            _tilemap.SetTileFlags(cell, flags);
        }

        private void RestoreColors()
        {
            foreach (FadingTile tile in _fadingTiles)
                if (_tilemap.GetTile(tile.Cell) == tile.Asset)
                    SetTileColor(tile.Cell, tile.OriginalColor);
            _fadingTiles.Clear();
        }

        private void OnDisable()
        {
            if (_tilemap != null) RestoreColors();
        }
    }
}
