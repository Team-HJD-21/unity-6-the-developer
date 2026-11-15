using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TilemapOuterBoundary : MonoBehaviour
    {
        [SerializeField] private TilemapCollider2D _source;

        private Tilemap _tilemap;
        private CompositeCollider2D _composite;
        private bool _rebuildPending;

        private void OnEnable()
        {
            if (_source == null || !_source.TryGetComponent(out _tilemap)
                || !_source.TryGetComponent(out _composite))
                return;

            Tilemap.tilemapPositionsChanged += OnTilesChanged;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                UnityEditor.Undo.undoRedoPerformed += QueueRebuild;
                UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            }
#endif
            QueueRebuild();
        }

        private void OnDisable()
        {
            Tilemap.tilemapPositionsChanged -= OnTilesChanged;
#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= QueueRebuild;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
            UnityEditor.EditorApplication.delayCall -= RebuildIfPending;
#endif
            _rebuildPending = false;
        }

        private void OnTilesChanged(Tilemap tilemap, NativeArray<Vector3Int> positions)
        {
            if (tilemap == _tilemap) QueueRebuild();
        }

        private void QueueRebuild()
        {
            if (_rebuildPending) return;

            _rebuildPending = true;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                UnityEditor.EditorApplication.delayCall += RebuildIfPending;
#endif
        }

        private void LateUpdate()
        {
            if (Application.IsPlaying(gameObject)) RebuildIfPending();
        }

#if UNITY_EDITOR
        private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (scene == gameObject.scene) RebuildIfPending();
        }
#endif

        private void RebuildIfPending()
        {
            if (!_rebuildPending) return;

            try
            {
                if (_source == null || !_source.gameObject.activeInHierarchy || _composite == null
                    || !_composite.enabled || _source.compositeOperation != Collider2D.CompositeOperation.Merge
                    || _composite.geometryType != CompositeCollider2D.GeometryType.Outlines)
                    return;

                UpdateEdges(ReadOutlines());
            }
            finally
            {
                _rebuildPending = false;
            }
        }

        private List<Outline> ReadOutlines()
        {
            var outlines = new List<Outline>();
            bool wasEnabled = _source.enabled;
            try
            {
                _source.enabled = true;
                _source.ProcessTilemapChanges();
                _composite.GenerateGeometry();
                for (int i = 0; i < _composite.pathCount; i++)
                {
                    int count = _composite.GetPathPointCount(i);
                    if (count < 3) continue;

                    var points = new Vector2[count];
                    _composite.GetPath(i, points);
                    var outline = new Outline(points, i);
                    if (outline.Area > 0d) outlines.Add(outline);
                }
            }
            finally
            {
                _source.enabled = wasEnabled;
                if (!wasEnabled && _composite.pathCount > 0) _composite.GenerateGeometry();
            }

            outlines.Sort((a, b) =>
            {
                int order = b.Area.CompareTo(a.Area);
                return order != 0 ? order : a.Index.CompareTo(b.Index);
            });
            return outlines;
        }

        private void UpdateEdges(List<Outline> outlines)
        {
            var exterior = new List<Outline>();
            foreach (var outline in outlines)
            {
                if (!exterior.Exists(parent => parent.Contains(outline))) exterior.Add(outline);
            }

            var edges = GetComponents<EdgeCollider2D>();
            var points = new List<Vector2>();
            Matrix4x4 sourceToBoundary = transform.worldToLocalMatrix * _source.transform.localToWorldMatrix;
            bool changed = false;
            for (int i = 0; i < exterior.Count; i++)
            {
                points.Clear();
                foreach (Vector2 sourcePoint in exterior[i].Points)
                {
                    Vector2 point = sourceToBoundary.MultiplyPoint3x4(sourcePoint);
                    if (points.Count == 0 || !points[points.Count - 1].Equals(point)) points.Add(point);
                }
                if (!points[0].Equals(points[points.Count - 1])) points.Add(points[0]);

                var edge = i < edges.Length ? edges[i] : gameObject.AddComponent<EdgeCollider2D>();
                if (Matches(edge, points)) continue;

                edge.SetPoints(points);
                edge.enabled = true;
                changed = true;
            }

            for (int i = exterior.Count; i < edges.Length; i++)
            {
                if (!edges[i].enabled) continue;
                edges[i].enabled = false;
                changed = true;
            }

#if UNITY_EDITOR
            if (changed && !Application.IsPlaying(gameObject) && gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private static bool Matches(EdgeCollider2D edge, List<Vector2> points)
        {
            if (!edge.enabled || edge.pointCount != points.Count) return false;

            var existing = edge.points;
            for (int i = 0; i < existing.Length; i++)
            {
                if (!existing[i].Equals(points[i])) return false;
            }
            return true;
        }

        private readonly struct Outline
        {
            public readonly Vector2[] Points;
            public readonly int Index;
            public readonly double Area;
            private readonly Rect _bounds;

            public Outline(Vector2[] points, int index)
            {
                Points = points;
                Index = index;
                Vector2 min = points[0];
                Vector2 max = points[0];
                double area = 0d;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[(i + 1) % points.Length];
                    area += (double)a.x * b.y - (double)b.x * a.y;
                    min = Vector2.Min(min, a);
                    max = Vector2.Max(max, a);
                }

                Area = Math.Abs(area) * 0.5d;
                _bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            public bool Contains(Outline other)
            {
                if (Area <= other.Area || _bounds.xMin > other._bounds.xMin || _bounds.yMin > other._bounds.yMin
                    || _bounds.xMax < other._bounds.xMax || _bounds.yMax < other._bounds.yMax)
                    return false;

                foreach (Vector2 point in other.Points)
                {
                    bool inside = false;
                    bool onEdge = false;
                    for (int i = 0; i < Points.Length; i++)
                    {
                        Vector2 a = Points[i];
                        Vector2 b = Points[(i + 1) % Points.Length];
                        double cross = ((double)b.x - a.x) * ((double)point.y - a.y)
                            - ((double)b.y - a.y) * ((double)point.x - a.x);
                        if (cross == 0d && point.x >= Mathf.Min(a.x, b.x) && point.x <= Mathf.Max(a.x, b.x)
                            && point.y >= Mathf.Min(a.y, b.y) && point.y <= Mathf.Max(a.y, b.y))
                        {
                            onEdge = true;
                            break;
                        }

                        if ((a.y > point.y) != (b.y > point.y) && (cross > 0d) == (b.y > a.y))
                            inside = !inside;
                    }

                    if (!onEdge) return inside;
                }

                return true;
            }
        }
    }
}
