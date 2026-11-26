using System;
using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed class ControlUnitDamageFx
    {
        private const int SmokeThreshold = 80;
        private const int FlameThreshold = 40;
        private const int HealthStep = 5;
        private const float SmokeScale = 1.08f;
        private const float FlameScale = 1.7f;
        private const string ExcludedPartName = "STATION2_DOWN_RIGHT";

        private struct Emitter
        {
            public SpriteRenderer Renderer;
            public double Phase;
        }

        private readonly Component _owner;
        private readonly System.Random _random;
        private readonly List<Surface> _surfaces = new();
        private readonly Emitter[] _smoke = new Emitter[1 + SmokeThreshold / HealthStep];
        private readonly Emitter[] _flames = new Emitter[1 + FlameThreshold / HealthStep];
        private FieldTurretStatusFx.Atlas _atlas;
        private bool _initialized;
        private int _smokeCount;
        private int _flameCount;
        private int _sortingLayer;
        private int _sortingOrder;

        public ControlUnitDamageFx(Component owner)
        {
            _owner = owner;
            _random = new System.Random(owner.GetInstanceID());
        }

        public void Update(int health, int maxHealth)
        {
            if (maxHealth <= 0 || health * 100L > maxHealth * (long)SmokeThreshold)
            {
                Clear();
                return;
            }

            if (!_initialized)
            {
                _initialized = true;
                _atlas = FieldTurretStatusFx.GetSharedAtlas();
                if (_atlas != null) FindSurfaces();
            }
            if (_surfaces.Count == 0) return;

            if (_smokeCount == 0)
            {
                for (int i = _surfaces.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (_surfaces[i], _surfaces[j]) = (_surfaces[j], _surfaces[i]);
                }
            }

            double now = FieldSimulationTime.Seconds(_owner);
            int smokeCount = CountAtThreshold(health, maxHealth, SmokeThreshold);
            int flameCount = CountAtThreshold(health, maxHealth, FlameThreshold);
            var smokeClip = health <= 0 ? _atlas.WreckSmoke : _atlas.Damaged;
            Render(_smoke, ref _smokeCount, smokeCount, smokeClip, now, flame: false);
            Render(_flames, ref _flameCount, flameCount, _atlas.WreckFlame, now, flame: true);
        }

        private static int CountAtThreshold(int health, int maxHealth, int threshold)
        {
            long belowThreshold = threshold * (long)maxHealth - 100L * Mathf.Max(0, health);
            return belowThreshold < 0 ? 0 : 1 + (int)(belowThreshold / (HealthStep * (long)maxHealth));
        }

        private void FindSurfaces()
        {
            int layerValue = int.MinValue;
            foreach (var renderer in _owner.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.enabled || renderer.sprite == null) continue;
                if (renderer.gameObject.name != ExcludedPartName)
                {
                    var surface = new Surface(renderer);
                    if (surface.HasArea) _surfaces.Add(surface);
                }

                int value = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                if (value > layerValue || value == layerValue && renderer.sortingOrder > _sortingOrder)
                {
                    layerValue = value;
                    _sortingLayer = renderer.sortingLayerID;
                    _sortingOrder = renderer.sortingOrder;
                }
            }
        }

        private void Render(Emitter[] sources, ref int previousCount, int count, FieldTurretStatusFx.Clip clip,
            double now, bool flame)
        {
            for (int i = 0; i < count; i++)
            {
                ref Emitter source = ref sources[i];
                if (source.Renderer == null)
                {
                    var root = _owner.transform;
                    var obj = new GameObject(flame ? "CU Status Flame" : "CU Status Smoke") { layer = root.gameObject.layer };
                    obj.transform.SetParent(root, false);
                    float scale = flame ? FlameScale : SmokeScale;
                    obj.transform.localScale = new Vector3(scale, scale, 1f);
                    source.Renderer = obj.AddComponent<SpriteRenderer>();
                    source.Renderer.sharedMaterial = _atlas.Material;
                    source.Renderer.sortingLayerID = _sortingLayer;
                    source.Renderer.sortingOrder = _sortingOrder + (flame ? 2 : 1);
                }
                if (i >= previousCount)
                {
                    // Spread the first sources across different CU parts before reusing a part.
                    int offset = flame ? _surfaces.Count / 2 : 0;
                    var surface = _surfaces[(i + offset) % _surfaces.Count];
                    source.Renderer.transform.position = surface.PickPoint(_random);
                    source.Phase = _random.NextDouble() * clip.Count / _atlas.Fps;
                    source.Renderer.enabled = true;
                }

                int frame = _atlas.GetFrameIndex(clip, now + source.Phase);
                if (frame < 0) continue;
                Sprite sprite = _atlas.GetSprite(clip.from + frame);
                if (source.Renderer.sprite != sprite) source.Renderer.sprite = sprite;
            }

            Hide(sources, count, previousCount);
            previousCount = count;
        }

        public void Clear()
        {
            Hide(_smoke, 0, _smokeCount);
            Hide(_flames, 0, _flameCount);
            _smokeCount = _flameCount = 0;
        }

        private static void Hide(Emitter[] sources, int start, int end)
        {
            for (int i = start; i < end; i++)
                if (sources[i].Renderer != null) sources[i].Renderer.enabled = false;
        }

        private sealed class Surface
        {
            private readonly SpriteRenderer _renderer;
            private readonly Vector2[] _vertices;
            private readonly ushort[] _indices;
            private readonly float[] _cumulativeAreas;
            private readonly float _area;

            public bool HasArea => _area > 0f;

            public Surface(SpriteRenderer renderer)
            {
                _renderer = renderer;
                _vertices = renderer.sprite.vertices;
                _indices = renderer.sprite.triangles;
                _cumulativeAreas = new float[_indices.Length / 3];
                for (int i = 0; i < _cumulativeAreas.Length; i++)
                {
                    int index = i * 3;
                    Vector2 a = _vertices[_indices[index]];
                    Vector2 b = _vertices[_indices[index + 1]] - a;
                    Vector2 c = _vertices[_indices[index + 2]] - a;
                    _area += Mathf.Abs(b.x * c.y - b.y * c.x);
                    _cumulativeAreas[i] = _area;
                }
            }

            public Vector3 PickPoint(System.Random random)
            {
                int triangle = Array.BinarySearch(_cumulativeAreas, (float)(random.NextDouble() * _area));
                int index = 3 * (triangle < 0 ? ~triangle : triangle);
                float u = (float)random.NextDouble();
                float v = (float)random.NextDouble();
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                Vector2 a = _vertices[_indices[index]];
                Vector2 point = a + u * (_vertices[_indices[index + 1]] - a) + v * (_vertices[_indices[index + 2]] - a);
                if (_renderer.flipX) point.x = -point.x;
                if (_renderer.flipY) point.y = -point.y;
                return _renderer.transform.TransformPoint(point);
            }
        }
    }
}
