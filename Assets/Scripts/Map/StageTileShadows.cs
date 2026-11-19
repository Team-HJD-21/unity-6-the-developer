using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Map
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StageTileShadows : MonoBehaviour
    {
        private const float NightTwilightFraction = 0.05f;
        private const float ShadowStep = 0.025f;

        [SerializeField] private GameObject _timeSource;
        [SerializeField] private Light2D _sun;
        [FormerlySerializedAs("_trees")]
        [SerializeField] private Tilemap _tilemap;
        [SerializeField, Min(0f)] private float _noonShadowLength = 0.8f;
        [SerializeField, Min(0f)] private float _horizonShadowLength = 1.36f;
        [SerializeField] private float _horizontalOffsetPixels;
        [SerializeField] private float _verticalOffsetPixels;

        private NativeArray<ShadowVertex> _projection;
        private NativeArray<Vector3> _vertices;
        private NativeArray<Color32> _colors;
        private Material[] _materials;
        private Matrix4x4 _tilemapToShadow;

        private IMatchDayNightCycle _cycle;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3 _initialSunPosition;
        private float _daylightIntensity;
        private Vector2 _lastProjectionOffset;
        private Vector2 _lastPositionOffsetPixels;
        private float _lastStrength;
        private bool _hasShadow;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.enabled = false;
            if (_timeSource == null || _sun == null || _tilemap == null || _renderer.sharedMaterial == null)
            {
                enabled = false;
                return;
            }

            _initialSunPosition = _sun.transform.localPosition;
            _daylightIntensity = _sun.intensity;
            _mesh = new Mesh { name = "Runtime tile shadows" };
            _mesh.MarkDynamic();
            ConfigureMesh(_renderer.sharedMaterial);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            if (_timeSource == null || _sun == null)
            {
                _renderer.enabled = false;
                return;
            }

            _cycle ??= _timeSource.GetComponent<IMatchDayNightCycle>();
            if (_cycle == null || _cycle.TimeOfDayId.IsEmpty) return;

            bool isDay = _cycle.Phase == DayNightPhase.Day;
            double duration = isDay ? _cycle.DaySeconds : _cycle.NightSeconds;
            double remaining = _cycle.RemainingSeconds;
            if (!double.IsFinite(duration) || duration <= 0d || !double.IsFinite(remaining)) return;

            float progress = Mathf.Clamp01((float)(1d - remaining / duration));
            Vector2 initialOffset = _initialSunPosition;
            float morningAngle = Mathf.Atan2(initialOffset.x, initialOffset.y);
            float eveningAngle = -morningAngle;
            float angle = isDay
                ? Mathf.Lerp(morningAngle, eveningAngle, progress)
                : Mathf.Lerp(eveningAngle, 2f * Mathf.PI + morningAngle, progress);
            float darkness = isDay ? 0f : Mathf.Clamp01(Mathf.Min(progress, 1f - progress) / NightTwilightFraction);
            float length = isDay
                ? Mathf.Lerp(_noonShadowLength, _horizonShadowLength, Mathf.Abs(2f * progress - 1f))
                : _horizonShadowLength;

            ApplySun(angle, initialOffset.magnitude, length, 1f - darkness);
        }

        private void ApplySun(float angle, float radius, float length, float strength)
        {
            Vector2 sunDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 sunPosition = sunDirection * radius;
            _sun.transform.localPosition = new Vector3(sunPosition.x, sunPosition.y, _initialSunPosition.z);
            _sun.intensity = _daylightIntensity * strength;

            _renderer.enabled = strength > 0f && _vertices.Length > 0;
            if (!_renderer.enabled) return;

            Vector3 worldDirection = _sun.transform.parent == null
                ? (Vector3)sunDirection : _sun.transform.parent.TransformDirection(sunDirection);
            Vector2 projectionOffset = transform.InverseTransformVector(-worldDirection * length);
            var positionOffsetPixels = new Vector2(_horizontalOffsetPixels, _verticalOffsetPixels);
            if (_hasShadow && (projectionOffset - _lastProjectionOffset).sqrMagnitude < ShadowStep * ShadowStep
                && Mathf.Abs(strength - _lastStrength) < ShadowStep
                && positionOffsetPixels.Equals(_lastPositionOffsetPixels)) return;

            Vector3 positionOffset = _tilemapToShadow.MultiplyVector(positionOffsetPixels);
            DrawShadows(projectionOffset, positionOffset, strength);
            _lastProjectionOffset = projectionOffset;
            _lastPositionOffsetPixels = positionOffsetPixels;
            _lastStrength = strength;
            _hasShadow = true;
        }

        private void ConfigureMesh(Material template)
        {
            var projection = new List<ShadowVertex>();
            var uv = new List<Vector2>();
            var submeshByTexture = new Dictionary<Texture2D, int>();
            var submeshes = new List<List<int>>();
            var materials = new List<Material>();
            var geometryBySprite = new Dictionary<Sprite, SpriteGeometry>();
            _tilemapToShadow = transform.worldToLocalMatrix * _tilemap.transform.localToWorldMatrix;

            foreach (Vector3Int cell in _tilemap.cellBounds.allPositionsWithin)
            {
                Sprite sprite = _tilemap.GetSprite(cell);
                if (sprite == null) continue;

                if (!geometryBySprite.TryGetValue(sprite, out SpriteGeometry geometry))
                {
                    geometry = new SpriteGeometry(sprite);
                    geometryBySprite.Add(sprite, geometry);
                }

                Texture2D texture = sprite.texture;
                if (!submeshByTexture.TryGetValue(texture, out int submesh))
                {
                    submesh = materials.Count;
                    submeshByTexture.Add(texture, submesh);
                    materials.Add(new Material(template) { mainTexture = texture });
                    submeshes.Add(new List<int>());
                }

                Matrix4x4 tileToShadow = _tilemapToShadow * Matrix4x4.Translate(_tilemap.GetCellCenterLocal(cell))
                    * _tilemap.orientationMatrix * _tilemap.GetTransformMatrix(cell);
                float unitsPerPixel = 1f / sprite.pixelsPerUnit;
                int firstVertex = projection.Count;
                foreach (Vector2 position in geometry.Positions)
                {
                    Vector3 ground = tileToShadow.MultiplyPoint3x4(new Vector3(position.x, geometry.GroundY, 0f));
                    float height = Mathf.Max(0f, position.y - geometry.GroundY) * geometry.HeightScale;
                    projection.Add(new ShadowVertex(ground, height, unitsPerPixel));
                }

                uv.AddRange(geometry.Uv);
                foreach (ushort index in geometry.Indices)
                    submeshes[submesh].Add(firstVertex + index);
            }

            _projection = new NativeArray<ShadowVertex>(projection.ToArray(), Allocator.Persistent);
            _vertices = new NativeArray<Vector3>(_projection.Length, Allocator.Persistent);
            _colors = new NativeArray<Color32>(_projection.Length, Allocator.Persistent);
            _materials = materials.ToArray();

            if (_vertices.Length > ushort.MaxValue) _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, uv);
            _mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
                _mesh.SetTriangles(submeshes[i], i, false);
            _renderer.sharedMaterials = _materials;
        }

        private void DrawShadows(Vector2 projectionOffset, Vector3 positionOffset, float strength)
        {
            var job = new ProjectShadowsJob
            {
                Projection = _projection,
                Vertices = _vertices,
                Colors = _colors,
                ProjectionOffset = projectionOffset,
                PositionOffset = positionOffset,
                Strength = strength
            };
            if (_projection.Length >= 4096)
                job.Schedule(_projection.Length, 128).Complete();
            else
                for (int index = 0; index < _projection.Length; index++)
                    job.Execute(index);

            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshUpdateFlags.DontRecalculateBounds);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();
        }

        [BurstCompile(FloatMode = FloatMode.Strict)]
        private struct ProjectShadowsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ShadowVertex> Projection;
            [WriteOnly] public NativeArray<Vector3> Vertices;
            [WriteOnly] public NativeArray<Color32> Colors;
            public Vector3 ProjectionOffset;
            public Vector3 PositionOffset;
            public float Strength;

            public void Execute(int index)
            {
                ShadowVertex point = Projection[index];
                Vertices[index] = point.Ground + ProjectionOffset * point.Height + PositionOffset * point.UnitsPerPixel;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(92f, 26f, point.Height) * Strength);
                Colors[index] = new Color32(0, 0, 0, alpha);
            }
        }

        private readonly struct ShadowVertex
        {
            public readonly Vector3 Ground;
            public readonly float Height;
            public readonly float UnitsPerPixel;

            public ShadowVertex(Vector3 ground, float height, float unitsPerPixel)
            {
                Ground = ground;
                Height = height;
                UnitsPerPixel = unitsPerPixel;
            }
        }

        private readonly struct SpriteGeometry
        {
            public readonly Vector2[] Positions;
            public readonly Vector2[] Uv;
            public readonly ushort[] Indices;
            public readonly float GroundY;
            public readonly float HeightScale;

            public SpriteGeometry(Sprite sprite)
            {
                Positions = sprite.vertices;
                Uv = sprite.uv;
                Indices = sprite.triangles;
                Bounds bounds = sprite.bounds;
                GroundY = bounds.max.y > 0f ? 0f : bounds.min.y;
                HeightScale = 1f / Mathf.Max(bounds.max.y - GroundY, 1f); // Short props cast proportionally shorter shadows.
            }
        }

        private void OnDisable()
        {
            if (_mesh == null) return;
            _renderer.enabled = false;
            _hasShadow = false;
            if (_sun == null) return;
            _sun.transform.localPosition = _initialSunPosition;
            _sun.intensity = _daylightIntensity;
        }

        private void OnDestroy()
        {
            if (_projection.IsCreated) _projection.Dispose();
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_colors.IsCreated) _colors.Dispose();
            if (_mesh != null) Destroy(_mesh);
            if (_materials == null) return;
            foreach (Material material in _materials)
                Destroy(material);
        }
    }
}
