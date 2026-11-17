using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    internal sealed class FieldTurretStatusFx
    {
        private const float PixelsPerUnit = 10f;
        private const int SmokeLimit = 4;
        private const int FireCount = 2;
        private const double VentGlowDuration = 1.1d;
        private const float PartHeatRisePerSecond = 2f;
        private const int GlowOrderOffset = 5;
        private const int MissileFrameSize = 128;
        private const int MissileFrameCount = 5;
        private const int MissileHeatFrameHeight = 48;
        private static readonly Vector3 GunVent = new(0f, -1.7f, 0f);
        private static readonly string[] SmokeNames =
        {
            "Turret Status Smoke", "Turret Status Smoke 2", "Turret Status Smoke 3", "Turret Status Smoke 4"
        };
        private static readonly string[] FireNames = { "Turret Status Flame", "Turret Status Flame 2" };
        private static readonly int AtlasTextureId = Shader.PropertyToID("_LedTexture");
        private static readonly int EmissionTextureId = Shader.PropertyToID("_EmissionTex");
        private static Atlas _sharedAtlas;

        private struct SmokeSource
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public double StartedAt;
            public int WreckFrame;
        }

        private readonly Atlas _atlas;
        private readonly SpriteRenderer _gun;
        private readonly Sprite _canonHeatParts;
        private readonly int _missileLevel;
        private SpriteRenderer _glow;
        private float _displayedPartHeat;
        private double? _partHeatUpdatedAt;
        private readonly System.Random _visualRandom;
        private readonly SmokeSource[] _smoke = new SmokeSource[SmokeLimit];
        private readonly SpriteRenderer[] _fire = new SpriteRenderer[FireCount];
        private readonly Vector3[] _fireOrigins = new Vector3[FireCount];
        private int _smokeCount;
        private double _wreckStartedAt;
        private double _recoveryStartedAt;
        private bool _wasDestroyed;
        private bool _recovering;

        public SpriteRenderer HeatPartsRenderer { get; private set; }
        public float HeatPartsStrength => _displayedPartHeat;

        private FieldTurretStatusFx(Atlas atlas, SpriteRenderer gun, FieldTurretKind kind, FieldTurretDefinition definition)
        {
            _atlas = atlas;
            _gun = gun;
            if (kind == FieldTurretKind.Canon)
                _canonHeatParts = atlas.GetCanonHeatParts(definition.Level);
            else if (kind == FieldTurretKind.Missile &&
                     definition.Id?.StartsWith("missile_stage1_", StringComparison.Ordinal) == true)
                _missileLevel = definition.Level;
            _visualRandom = new System.Random(gun.GetInstanceID());
        }

        public static FieldTurretStatusFx Create(SpriteRenderer gun, FieldTurretKind kind, FieldTurretDefinition definition)
        {
            _sharedAtlas ??= Atlas.Load();
            return _sharedAtlas == null ? null : new FieldTurretStatusFx(_sharedAtlas, gun, kind, definition);
        }

        public void Update(double now, bool destroyed, int damagedSmokeCount, float heatRatio, double overheatStartedAt)
        {
            HeatPartsRenderer = null;
            if (destroyed)
            {
                Hide(_glow);
                _displayedPartHeat = 0f;
                _partHeatUpdatedAt = null;
                UpdateWreck(now);
                return;
            }

            if (_wasDestroyed)
            {
                _wasDestroyed = false;
                _recovering = true;
                _recoveryStartedAt = now;
            }
            UpdateGlow(now, heatRatio, now - overheatStartedAt);

            if (_recovering)
            {
                int frame = FrameIndex(_atlas.Recovery[0], now - _recoveryStartedAt);
                if (frame >= 0)
                {
                    for (int i = 0; i < _smokeCount; i++)
                    {
                        var clip = _atlas.Recovery[_smoke[i].WreckFrame];
                        double durationScale = 1d - .17d * i;
                        DrawSmoke(i, clip, FrameIndex(clip, (now - _recoveryStartedAt) / durationScale));
                    }
                    return;
                }

                _recovering = false;
            }

            SetSmokeCount(damagedSmokeCount, now);
            for (int i = 0; i < _smokeCount; i++)
                DrawSmoke(i, _atlas.Damaged, FrameIndex(_atlas.Damaged, now - _smoke[i].StartedAt));
        }

        private void UpdateWreck(double now)
        {
            if (!_wasDestroyed)
            {
                _wasDestroyed = true;
                _recovering = false;
                _wreckStartedAt = now;
                SetSmokeCount(SmokeLimit, now);
            }

            double elapsed = now - _wreckStartedAt;
            for (int i = 0; i < _smokeCount; i++)
            {
                int frame = FrameIndex(_atlas.WreckSmoke, elapsed + 5d * i / _atlas.Fps);
                _smoke[i].WreckFrame = Mathf.Max(0, frame);
                DrawSmoke(i, _atlas.WreckSmoke, frame);
            }
            for (int i = 0; i < FireCount; i++)
            {
                int frame = FrameIndex(_atlas.WreckFlame, elapsed + 7d * i / _atlas.Fps);
                Draw(ref _fire[i], FireNames[i], _atlas.WreckFlame, frame, 3, _fireOrigins[i], .85f);
            }
        }

        private void UpdateGlow(double now, float heatRatio, double flashAge)
        {
            if (_canonHeatParts != null || _missileLevel > 0)
            {
                float rise = _partHeatUpdatedAt.HasValue
                    ? (float)Math.Max(0d, now - _partHeatUpdatedAt.Value) * PartHeatRisePerSecond
                    : 1f;
                _displayedPartHeat = Mathf.Min(heatRatio, _displayedPartHeat + rise);
                _partHeatUpdatedAt = now;
                if (_displayedPartHeat <= 0f)
                {
                    Hide(_glow);
                    return;
                }
            }

            Sprite parts = _canonHeatParts;
            Material partsMaterial = _atlas.CanonHeatMaterial;
            Vector3 origin = Vector3.zero;
            float scale = 1f;
            if (_missileLevel > 0 && _gun.sprite != null)
            {
                Sprite gun = _gun.sprite;
                Rect rect = gun.rect;
                parts = _atlas.GetMissileHeatParts(_missileLevel, (int)(rect.x / MissileFrameSize));
                partsMaterial = _atlas.MissileHeatMaterial;
                // Trimmed SpriteRects have their pivot relative to the imported crop.
                float center = MissileFrameSize * .5f;
                origin = new Vector3(center - rect.x % MissileFrameSize - gun.pivot.x,
                    center - rect.y - gun.pivot.y, 0f) / gun.pixelsPerUnit;
                scale = PixelsPerUnit / gun.pixelsPerUnit;
            }

            if (parts != null)
            {
                float opacity = _displayedPartHeat * (2f - _displayedPartHeat);
                DrawSprite(ref _glow, "Turret Overheat", parts, partsMaterial,
                    GlowOrderOffset, origin, scale, opacity, mirrorSprite: true);
                HeatPartsRenderer = _glow;
                return;
            }

            if (flashAge < 0d || flashAge >= VentGlowDuration)
            {
                Hide(_glow);
                return;
            }

            int frame = (int)(flashAge / VentGlowDuration * _atlas.VentGlow.Count);
            var sprite = _atlas.GetSprite(_atlas.VentGlow.from + frame);
            DrawSprite(ref _glow, "Turret Overheat", sprite, _atlas.VentGlowMaterial, GlowOrderOffset, GunVent, 1.7f);
        }

        private void SetSmokeCount(int count, double now)
        {
            if (_smokeCount == count) return;
            if (_smokeCount == 0) PickOrigins();

            for (int i = _smokeCount; i < count; i++)
                _smoke[i].StartedAt = now;
            for (int i = count; i < _smokeCount; i++)
                Hide(_smoke[i].Renderer);
            _smokeCount = count;
        }

        private void PickOrigins()
        {
            // Six non-overlapping cells inside the opaque area shared by Stage1 gun sprites.
            int[] cells = { 0, 1, 2, 3, 4, 5 };
            float pixelsPerUnit = _gun.sprite != null ? _gun.sprite.pixelsPerUnit : PixelsPerUnit;
            for (int i = 0; i < cells.Length; i++)
            {
                int pick = _visualRandom.Next(i, cells.Length);
                int cell = cells[pick];
                cells[pick] = cells[i];

                float x = ((cell % 3 - 1) * 9 + _visualRandom.Next(-2, 3)) / pixelsPerUnit;
                float y = ((cell < 3 ? -8 : 8) + _visualRandom.Next(-2, 3)) / pixelsPerUnit;
                var origin = new Vector3(x, y, 0f);
                if (i < SmokeLimit) _smoke[i].Origin = origin;
                else _fireOrigins[i - SmokeLimit] = origin;
            }
        }

        private void DrawSmoke(int index, Clip clip, int frame)
        {
            ref SmokeSource source = ref _smoke[index];
            Draw(ref source.Renderer, SmokeNames[index], clip, frame, 2, source.Origin, .72f);
        }

        private static void Hide(SpriteRenderer renderer)
        {
            if (renderer != null && renderer.enabled) renderer.enabled = false;
        }

        public void HideFire()
        {
            foreach (var renderer in _fire) Hide(renderer);
        }

        public void Reset()
        {
            SetSmokeCount(0, 0d);
            HideFire();
            Hide(_glow);
            HeatPartsRenderer = null;
            _displayedPartHeat = 0f;
            _partHeatUpdatedAt = null;
            _wasDestroyed = false;
            _recovering = false;
        }

        public void RebaseTime(double offset)
        {
            for (int i = 0; i < _smokeCount; i++)
                _smoke[i].StartedAt += offset;
            _wreckStartedAt += offset;
            _recoveryStartedAt += offset;
            _partHeatUpdatedAt += offset;
        }

        public void Encapsulate(ref Bounds bounds)
        {
            for (int i = 0; i < _smokeCount; i++)
            {
                var renderer = _smoke[i].Renderer;
                if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
            }
            foreach (var renderer in _fire)
                if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
            if (_glow != null && _glow.enabled) bounds.Encapsulate(_glow.bounds);
        }

        public void ApplyGunMask()
        {
            for (int i = 0; i < _smokeCount; i++)
                SetMask(_smoke[i].Renderer);
            foreach (var renderer in _fire) SetMask(renderer);
            SetMask(_glow);
        }

        private void SetMask(SpriteRenderer renderer)
        {
            if (renderer != null && renderer.maskInteraction != _gun.maskInteraction)
                renderer.maskInteraction = _gun.maskInteraction;
        }

        private int FrameIndex(Clip clip, double elapsed)
        {
            if (elapsed < 0 || double.IsInfinity(elapsed)) return -1;
            double frame = Math.Floor(elapsed * _atlas.Fps);
            if (clip.loop) return (int)(frame % clip.Count);
            return frame < clip.Count ? (int)frame : -1;
        }

        private void Draw(ref SpriteRenderer renderer, string name, Clip clip, int frame, int orderOffset,
            Vector3 origin, float scale)
        {
            if (frame < 0)
            {
                Hide(renderer);
                return;
            }

            DrawSprite(ref renderer, name, _atlas.GetSprite(clip.from + frame), _atlas.Material,
                orderOffset, origin, scale);
        }

        private void DrawSprite(ref SpriteRenderer renderer, string name, Sprite sprite, Material material,
            int orderOffset, Vector3 origin, float scale, float opacity = 1f, bool mirrorSprite = false)
        {
            if (renderer == null)
            {
                var obj = new GameObject(name) { layer = _gun.gameObject.layer };
                obj.transform.SetParent(_gun.transform, false); // The gun's SortingGroup owns the tree masks.
                renderer = obj.AddComponent<SpriteRenderer>();
            }

            if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;

            origin.x *= _gun.flipX ? -1f : 1f;
            origin.y *= _gun.flipY ? -1f : 1f;
            renderer.transform.localPosition = origin;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            renderer.flipX = mirrorSprite && _gun.flipX;
            renderer.flipY = mirrorSprite && _gun.flipY;
            renderer.color = new Color(1f, 1f, 1f, opacity);
            if (renderer.sprite != sprite) renderer.sprite = sprite;
            renderer.sortingLayerID = _gun.sortingLayerID;
            renderer.sortingOrder = _gun.sortingOrder + orderOffset;
            renderer.enabled = true;
        }

        private sealed class Atlas
        {
            private readonly AtlasData _data;
            private readonly Texture2D _texture;
            private readonly Sprite[] _sprites;

            private readonly Sprite[] _canonHeatParts = new Sprite[3];
            private readonly Sprite[,] _missileHeatParts = new Sprite[3, MissileFrameCount];

            public readonly Material Material;
            public readonly Material CanonHeatMaterial;
            public readonly Material MissileHeatMaterial;
            public readonly Material VentGlowMaterial;
            public readonly Clip Damaged;
            public readonly Clip WreckSmoke;
            public readonly Clip WreckFlame;
            public readonly Clip VentGlow;
            public readonly Clip[] Recovery;
            public int Fps => _data.meta.pixelfx.fps;

            private Atlas(AtlasData data, Texture2D texture, Shader shader, Dictionary<string, Clip> clips,
                Texture2D canonParts, Texture2D missileParts, Shader emissionShader)
            {
                _data = data;
                _texture = texture;
                _sprites = new Sprite[data.frames.Length];
                Damaged = clips["damaged"];
                WreckSmoke = clips["wreckSmoke"];
                WreckFlame = clips["wreckFlame"];
                VentGlow = clips["ventGlow"];
                Recovery = new Clip[WreckSmoke.Count];
                for (int index = 0; index < Recovery.Length; index++)
                    Recovery[index] = clips["recovery" + index];

                Material = new Material(shader) { name = "Turret Status Sprite Atlas", hideFlags = HideFlags.DontSave };
                Material.SetTexture(AtlasTextureId, texture); // Use the same mask-safe texture binding as the LED.
                CanonHeatMaterial = new Material(emissionShader) { name = "Canon Heat Parts", hideFlags = HideFlags.DontSave };
                CanonHeatMaterial.SetTexture(EmissionTextureId, canonParts);
                MissileHeatMaterial = new Material(emissionShader) { name = "Missile Heat Parts", hideFlags = HideFlags.DontSave };
                MissileHeatMaterial.SetTexture(EmissionTextureId, missileParts);
                VentGlowMaterial = new Material(emissionShader) { name = "Turret Vent Glow", hideFlags = HideFlags.DontSave };
                VentGlowMaterial.SetTexture(EmissionTextureId, texture);

                // Each crop is x 42..85, y 46..79 on the original 128×128 cannon frame.
                Vector2 pivot = new(.5f, 16f / 34f);
                for (int i = 0; i < _canonHeatParts.Length; i++)
                {
                    var sprite = Sprite.Create(canonParts, new Rect(i * 44, 0, 44, 34), pivot,
                        PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"canonHeat_Lv{i + 1}";
                    sprite.hideFlags = HideFlags.DontSave;
                    _canonHeatParts[i] = sprite;
                }

                // Missile tiles contain y 43..90 of each original 128×128 frame.
                Vector2 missilePivot = new(.5f, 27f / MissileHeatFrameHeight);
                for (int level = 0; level < _missileHeatParts.GetLength(0); level++)
                for (int frame = 0; frame < MissileFrameCount; frame++)
                {
                    Rect rect = new(frame * MissileFrameSize, missileParts.height - (level + 1) * MissileHeatFrameHeight,
                        MissileFrameSize, MissileHeatFrameHeight);
                    var sprite = Sprite.Create(missileParts, rect, missilePivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"missileHeat_Lv{level + 1}_{frame}";
                    sprite.hideFlags = HideFlags.DontSave;
                    _missileHeatParts[level, frame] = sprite;
                }
            }

            public static Atlas Load()
            {
                var texture = Resources.Load<Texture2D>("FieldTurretStatusFx");
                var frames = Resources.Load<TextAsset>("FieldTurretStatusFxFrames");
                var shader = Resources.Load<Shader>("FieldTurretLed");
                var canonParts = Resources.Load<Texture2D>("FieldCanonHeatParts");
                var missileParts = Resources.Load<Texture2D>("FieldMissileHeatParts");
                var emissionShader = Resources.Load<Shader>("FieldTurretHeatEmission");
                if (texture == null || frames == null || shader == null || canonParts == null || missileParts == null || emissionShader == null)
                {
                    Debug.LogError("Field turret status VFX resources are missing.");
                    return null;
                }

                var data = JsonUtility.FromJson<AtlasData>(frames.text);
                var clips = new Dictionary<string, Clip>(StringComparer.Ordinal);
                foreach (Clip clip in data.meta.frameTags) clips.Add(clip.name, clip);
                return new Atlas(data, texture, shader, clips, canonParts, missileParts, emissionShader);
            }

            public Sprite GetCanonHeatParts(int level) =>
                level >= 1 && level <= _canonHeatParts.Length ? _canonHeatParts[level - 1] : null;

            public Sprite GetMissileHeatParts(int level, int frame) =>
                level >= 1 && level <= _missileHeatParts.GetLength(0) && frame >= 0 && frame < MissileFrameCount
                    ? _missileHeatParts[level - 1, frame] : null;

            public Sprite GetSprite(int index)
            {
                if (_sprites[index] != null) return _sprites[index];
                Frame frame = _data.frames[index];
                FrameRect rect = frame.frame;
                int[] pivot = _data.meta.pixelfx.pivot;
                var sprite = Sprite.Create(_texture, new Rect(rect.x, _texture.height - rect.y - rect.h, rect.w, rect.h),
                    new Vector2((float)pivot[0] / rect.w, 1f - (float)pivot[1] / rect.h),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = frame.filename;
                sprite.hideFlags = HideFlags.DontSave;
                _sprites[index] = sprite;
                return sprite;
            }
        }

        [Serializable] private sealed class AtlasData { public Frame[] frames; public AtlasMeta meta; }
        [Serializable] private sealed class AtlasMeta { public Clip[] frameTags; public PixelFx pixelfx; }
        [Serializable] private sealed class PixelFx { public int fps; public int[] pivot; }
        [Serializable] private sealed class Frame { public string filename; public FrameRect frame; }
        [Serializable] private struct FrameRect { public int x, y, w, h; }
        [Serializable] private sealed class Clip
        {
            public string name;
            public int from, to;
            public bool loop;
            public int Count => to - from + 1;
        }
    }
}
