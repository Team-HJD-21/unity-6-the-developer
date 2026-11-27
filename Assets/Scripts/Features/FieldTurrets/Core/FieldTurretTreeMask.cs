using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets
{
    internal sealed class FieldTurretTreeMask : IDisposable
    {
        private readonly Transform _turret;
        private readonly SpriteRenderer _gun;
        private readonly SpriteRenderer _ledRenderer;
        private readonly Light2D _ledLight;
        private readonly float _ledLightLocalRadius;
        private readonly List<TilemapRenderer> _tilemaps = new();
        private readonly List<float> _tileMargins = new();
        private readonly List<(Sprite Sprite, Matrix4x4 ToWorld, Bounds Bounds)> _trees = new();
        private readonly List<SpriteMask> _masks = new();
        private readonly List<SpriteRenderer> _ledShadows = new();
        private readonly SpriteMaskInteraction _previousMaskInteraction;
        private readonly SpriteMaskInteraction _previousLedMaskInteraction;
        private Bounds _searchBounds;
        private Bounds _lastGunBounds;
        private Bounds _lastLedLightBounds;
        private bool _lastLedShadowsEnabled;
        private Matrix4x4 _lastGunToWorld;
        private float _lastTreeSortY;
        private bool _masksPositioned;
        private bool _needsRefresh = true;

        public FieldTurretTreeMask(Transform turret, SpriteRenderer gun)
        {
            _turret = turret;
            _gun = gun;
            FieldTurretLedIndicator indicator = gun.GetComponentInChildren<FieldTurretLedIndicator>(true);
            if (indicator != null && indicator.TryGetComponent(out SpriteRenderer ledRenderer))
            {
                _ledRenderer = ledRenderer;
                _previousLedMaskInteraction = ledRenderer.maskInteraction;
                _ledLight = indicator.Light;
                if (_ledLight != null && _ledLight.lightType == Light2D.LightType.Freeform)
                {
                    foreach (Vector3 point in _ledLight.shapePath)
                        _ledLightLocalRadius = Mathf.Max(_ledLightLocalRadius, point.magnitude);
                    _ledLightLocalRadius += _ledLight.shapeLightFalloffSize;
                }
            }

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
            if (_ledLightLocalRadius > 0f) _ledLight.shadowIntensity = 1f;
            Tilemap.tilemapTileChanged += OnTilemapChanged;
        }

        public void Update(Bounds bounds)
        {
            if (_tilemaps.Count == 0) return;
            if (!_gun.enabled || _gun.forceRenderingOff || _gun.sprite == null)
            {
                DisableMasksFrom(0);
                DisableLedShadowsFrom(0);
                SetMaskInteraction(false);
                _masksPositioned = false;
                return;
            }

            if (_ledRenderer != null && _ledRenderer.enabled && _ledRenderer.gameObject.activeInHierarchy &&
                !_ledRenderer.forceRenderingOff && _ledRenderer.sprite != null)
                bounds.Encapsulate(_ledRenderer.bounds);

            bool ledShadowsEnabled = _ledLightLocalRadius > 0f && _ledLight != null && _ledLight.isActiveAndEnabled && _ledLight.shadowsEnabled;
            Bounds ledLightBounds = default;
            Bounds queryBounds = bounds;
            if (ledShadowsEnabled)
            {
                Vector3 scale = _ledLight.transform.lossyScale;
                float diameter = 2f * _ledLightLocalRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                ledLightBounds = new Bounds(_ledLight.transform.position, new Vector3(diameter, diameter, 0f));
                queryBounds.Encapsulate(ledLightBounds);
            }

            float turretY = _turret.position.y;
            bool refresh = _needsRefresh || turretY != _lastTreeSortY ||
                !_searchBounds.Contains(queryBounds.min) || !_searchBounds.Contains(queryBounds.max);
            Matrix4x4 gunToWorld = _gun.transform.localToWorldMatrix;
            if (!refresh && _masksPositioned && gunToWorld == _lastGunToWorld && bounds == _lastGunBounds &&
                ledShadowsEnabled == _lastLedShadowsEnabled && ledLightBounds == _lastLedLightBounds) return;
            if (refresh) RefreshTrees(queryBounds, turretY);

            _lastGunToWorld = gunToWorld;
            _lastGunBounds = bounds;
            _lastLedLightBounds = ledLightBounds;
            _lastLedShadowsEnabled = ledShadowsEnabled;
            _masksPositioned = true;
            Matrix4x4 worldToGun = _gun.transform.worldToLocalMatrix;
            int used = 0;
            int usedLedShadows = 0;
            foreach (var tree in _trees)
            {
                bool masksTurret = OverlapsInXY(bounds, tree.Bounds);
                bool shadowsLed = ledShadowsEnabled && OverlapsInXY(ledLightBounds, tree.Bounds);
                if (!masksTurret && !shadowsLed) continue;

                Matrix4x4 local = worldToGun * tree.ToWorld;
                if (masksTurret)
                {
                    SpriteMask mask = used < _masks.Count ? _masks[used] : AddMask();
                    SetTreePose(mask.transform, local);
                    mask.sprite = tree.Sprite;
                    mask.enabled = true;
                    used++;
                }
                if (shadowsLed)
                {
                    SpriteRenderer shadow = usedLedShadows < _ledShadows.Count ? _ledShadows[usedLedShadows] : AddLedShadow();
                    SetTreePose(shadow.transform, local);
                    shadow.sprite = tree.Sprite;
                    shadow.gameObject.SetActive(true);
                    usedLedShadows++;
                }
            }

            DisableMasksFrom(used);
            DisableLedShadowsFrom(usedLedShadows);
            SetMaskInteraction(used > 0);
        }

        private static void SetTreePose(Transform target, Matrix4x4 local)
        {
            Vector3 right = local.GetColumn(0);
            Vector3 up = local.GetColumn(1);
            Vector3 position = local.GetColumn(3);
            float angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            float scaleY = Vector3.Cross(right, up).z < 0f ? -up.magnitude : up.magnitude;
            target.SetLocalPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, angle));
            target.localScale = new Vector3(right.magnitude, scaleY, 1f);
        }

        private void SetMaskInteraction(bool hasTreeOverlap)
        {
            SpriteMaskInteraction interaction = hasTreeOverlap
                ? SpriteMaskInteraction.VisibleOutsideMask
                : _previousMaskInteraction;
            if (_gun.maskInteraction != interaction) _gun.maskInteraction = interaction;
            if (_ledRenderer == null) return;

            SpriteMaskInteraction ledInteraction = hasTreeOverlap
                ? SpriteMaskInteraction.VisibleOutsideMask
                : _previousLedMaskInteraction;
            if (_ledRenderer.maskInteraction != ledInteraction)
                _ledRenderer.maskInteraction = ledInteraction;
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

        private SpriteRenderer AddLedShadow()
        {
            var root = new GameObject("LED Tree Shadow") { layer = _gun.gameObject.layer };
            root.SetActive(false);
            root.transform.SetParent(_gun.transform, false);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = _gun.sharedMaterial;
            renderer.forceRenderingOff = true;
            var caster = root.AddComponent<ShadowCaster2D>();
            caster.castingOption = ShadowCaster2D.ShadowCastingOptions.SelfShadow; // 잎 실루엣 내부만 가리고 포신 쪽으로 그림자를 늘리지 않습니다.
            caster.alphaCutoff = 0.5f;
            _ledShadows.Add(renderer);
            return renderer;
        }

        private void DisableLedShadowsFrom(int index)
        {
            for (; index < _ledShadows.Count; index++)
                _ledShadows[index].gameObject.SetActive(false);
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
            foreach (SpriteRenderer shadow in _ledShadows)
            {
                if (shadow == null) continue;
                shadow.gameObject.SetActive(false);
                Object.Destroy(shadow.gameObject);
            }
            if (_gun != null) _gun.maskInteraction = _previousMaskInteraction;
            if (_ledRenderer != null) _ledRenderer.maskInteraction = _previousLedMaskInteraction;
        }
    }
}
