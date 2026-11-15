using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets
{
    internal sealed class FieldTurretTreeMask : IDisposable
    {
        private readonly Transform _turret;
        private readonly SpriteRenderer _gun;
        private readonly List<TilemapRenderer> _tilemaps = new();
        private readonly List<float> _tileMargins = new();
        private readonly List<(Sprite Sprite, Matrix4x4 ToWorld, Bounds Bounds)> _trees = new();
        private readonly List<SpriteMask> _masks = new();
        private readonly SpriteMaskInteraction _previousMaskInteraction;
        private Bounds _searchBounds;
        private Bounds _lastGunBounds;
        private Matrix4x4 _lastGunToWorld;
        private float _lastTreeSortY;
        private bool _masksPositioned;
        private bool _needsRefresh = true;

        public FieldTurretTreeMask(Transform turret, SpriteRenderer gun)
        {
            _turret = turret;
            _gun = gun;
            foreach (TilemapRenderer renderer in Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != turret.gameObject.scene ||
                    renderer.mode != TilemapRenderer.Mode.Individual ||
                    renderer.sortingLayerName != "Entities" || renderer.sortingOrder != 0)
                    continue;

                _tilemaps.Add(renderer);
                _tileMargins.Add(GetTreeMargin(renderer.GetComponent<Tilemap>()));
            }

            _previousMaskInteraction = gun.maskInteraction;
            if (_tilemaps.Count == 0) return;

            Tilemap.tilemapTileChanged += OnTilemapChanged;
        }

        public void Update(Bounds bounds)
        {
            if (_tilemaps.Count == 0) return;
            if (!_gun.enabled || _gun.forceRenderingOff || _gun.sprite == null)
            {
                DisableMasksFrom(0);
                SetMaskInteraction(false);
                _masksPositioned = false;
                return;
            }

            float turretY = _turret.position.y;
            bool refresh = _needsRefresh || turretY != _lastTreeSortY ||
                !_searchBounds.Contains(bounds.min) || !_searchBounds.Contains(bounds.max);
            Matrix4x4 gunToWorld = _gun.transform.localToWorldMatrix;
            if (!refresh && _masksPositioned && gunToWorld == _lastGunToWorld && bounds == _lastGunBounds) return;
            if (refresh) RefreshTrees(bounds, turretY);

            _lastGunToWorld = gunToWorld;
            _lastGunBounds = bounds;
            _masksPositioned = true;
            Matrix4x4 worldToGun = _gun.transform.worldToLocalMatrix;
            int used = 0;
            foreach (var tree in _trees)
            {
                if (!OverlapsInXY(bounds, tree.Bounds)) continue;

                SpriteMask mask = used < _masks.Count ? _masks[used] : AddMask();
                Matrix4x4 local = worldToGun * tree.ToWorld;
                Vector3 right = local.GetColumn(0);
                Vector3 up = local.GetColumn(1);
                Vector3 position = local.GetColumn(3);
                float angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
                float scaleY = Vector3.Cross(right, up).z < 0f ? -up.magnitude : up.magnitude;
                mask.transform.SetLocalPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, angle));
                mask.transform.localScale = new Vector3(right.magnitude, scaleY, 1f);
                mask.sprite = tree.Sprite;
                mask.enabled = true;
                used++;
            }

            DisableMasksFrom(used);
            SetMaskInteraction(used > 0);
        }

        private void SetMaskInteraction(bool hasTreeOverlap)
        {
            SpriteMaskInteraction interaction = hasTreeOverlap
                ? SpriteMaskInteraction.VisibleOutsideMask
                : _previousMaskInteraction;
            if (_gun.maskInteraction != interaction) _gun.maskInteraction = interaction;
        }

        private void RefreshTrees(Bounds bounds, float turretY)
        {
            _lastTreeSortY = turretY;
            _searchBounds = bounds;
            _searchBounds.Expand(2f);
            _needsRefresh = false;
            _trees.Clear();
            for (int index = 0; index < _tilemaps.Count; index++)
            {
                TilemapRenderer renderer = _tilemaps[index];
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;

                Tilemap map = renderer.GetComponent<Tilemap>();
                float margin = _tileMargins[index];
                Vector3 padding = new Vector3(margin, margin, 0f);
                Vector3Int min = map.WorldToCell(_searchBounds.min - padding);
                Vector3Int max = map.WorldToCell(_searchBounds.max + padding);
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        Sprite sprite = map.GetSprite(cell);
                        if (!IsTree(sprite) || map.GetColor(cell).a <= 0f) continue;

                        Vector3 anchor = map.GetCellCenterWorld(cell);
                        if (anchor.y >= turretY) continue;

                        Matrix4x4 toWorld = map.transform.localToWorldMatrix *
                            Matrix4x4.Translate(map.GetCellCenterLocal(cell)) * map.orientationMatrix *
                            map.GetTransformMatrix(cell);
                        Bounds spriteBounds = sprite.bounds;
                        Vector3 extentX = toWorld.MultiplyVector(Vector3.right * spriteBounds.extents.x);
                        Vector3 extentY = toWorld.MultiplyVector(Vector3.up * spriteBounds.extents.y);
                        Vector3 center = toWorld.MultiplyPoint3x4(spriteBounds.center);
                        Vector3 size = new Vector3(
                            2f * (Mathf.Abs(extentX.x) + Mathf.Abs(extentY.x)),
                            2f * (Mathf.Abs(extentX.y) + Mathf.Abs(extentY.y)),
                            0f);
                        var treeBounds = new Bounds(center, size);
                        if (OverlapsInXY(_searchBounds, treeBounds)) _trees.Add((sprite, toWorld, treeBounds));
                    }
                }
            }
        }

        private static bool OverlapsInXY(Bounds first, Bounds second)
        {
            Vector3 separation = first.center - second.center;
            Vector3 extents = first.extents + second.extents;
            return Mathf.Abs(separation.x) <= extents.x && Mathf.Abs(separation.y) <= extents.y;
        }

        private void OnTilemapChanged(Tilemap map, Tilemap.SyncTile[] changes)
        {
            int index = _tilemaps.FindIndex(renderer => renderer != null && renderer.gameObject == map.gameObject);
            if (index < 0) return;

            _tileMargins[index] = GetTreeMargin(map);
            _needsRefresh = true;
        }

        private static float GetTreeMargin(Tilemap map)
        {
            Sprite[] sprites = new Sprite[map.GetUsedSpritesCount()];
            int count = map.GetUsedSpritesNonAlloc(sprites);
            float radius = 0f;
            for (int index = 0; index < count; index++)
            {
                Sprite sprite = sprites[index];
                if (!IsTree(sprite)) continue;
                radius = Mathf.Max(radius, sprite.bounds.center.magnitude + sprite.bounds.extents.magnitude);
            }
            Vector3 scale = map.transform.lossyScale;
            return radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private static bool IsTree(Sprite sprite)
            => sprite != null && sprite.name.StartsWith("Overworld_Trees_", StringComparison.Ordinal);

        private SpriteMask AddMask()
        {
            var obj = new GameObject("Tree Occlusion") { layer = _gun.gameObject.layer };
            obj.transform.SetParent(_gun.transform, false);
            var mask = obj.AddComponent<SpriteMask>();
            mask.alphaCutoff = 0.5f;
            _masks.Add(mask);
            return mask;
        }

        private void DisableMasksFrom(int index)
        {
            for (; index < _masks.Count; index++)
                _masks[index].enabled = false;
        }

        public void Dispose()
        {
            Tilemap.tilemapTileChanged -= OnTilemapChanged;
            foreach (SpriteMask mask in _masks)
            {
                if (mask == null) continue;
                mask.enabled = false;
                Object.Destroy(mask.gameObject);
            }
            if (_gun != null) _gun.maskInteraction = _previousMaskInteraction;
        }
    }
}
