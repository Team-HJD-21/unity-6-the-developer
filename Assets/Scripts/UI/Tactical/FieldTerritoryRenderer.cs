// 일반 Field의 점유 경계를 지형 타일 위·다른 월드 객체 아래에 표시합니다.
// 지도와 같은 RegionBoundaryGeometry를 읽고, 점유 안쪽은 초록·바깥쪽은 적색으로 표현합니다.
// Snapshot 변경 때만 메시를 갱신하며 점유·전투·네트워크 상태를 변경하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class FieldTerritoryRenderer : MonoBehaviour
{
    public const string SortingLayerName = "Territory";
    private TacticalCommandController _owner;
    private GameObject _surface;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private Material _material;
    private RegionGridSnapshot _snapshot;
    private float _innerWidth, _outerWidth;
    public MeshRenderer SurfaceRenderer => _renderer;
    public RegionGridSnapshot Snapshot => _snapshot;
    public IReadOnlyList<RegionBoundarySegment> BoundarySegments { get; private set; } = Array.Empty<RegionBoundarySegment>();

    /// <summary>TacticalCommandController.Awake에서 한 번 연결합니다. HUD와 함께 생성·해제됩니다.</summary>
    public static FieldTerritoryRenderer Attach(TacticalCommandController owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var view = owner.GetComponent<FieldTerritoryRenderer>();
        if (view == null) view = owner.gameObject.AddComponent<FieldTerritoryRenderer>();
        view._owner = owner;
        return view;
    }

    private void LateUpdate()
    {
        var snapshot = _owner != null ? _owner.Regions : null;
        bool visible = _owner != null && _owner.isActiveAndEnabled && _owner.Source != null &&
            _owner.Source.HasFieldActor && !_owner.IsMapPresented && snapshot != null && snapshot.CapturedCellCount > 0;
        if (!visible)
        {
            if (_renderer != null) _renderer.enabled = false;
            return;
        }
        if (_surface == null && !CreateSurface()) return;

        // 작은 셀에서도 안팎 띠가 반대편 경계를 넘지 않도록 폭을 제한합니다.
        var grid = snapshot.Definition.Geometry;
        float limit = (float)Math.Min(grid.CellWidth, grid.CellHeight) * .45f;
        float inner = Mathf.Min(limit, Mathf.Max(.001f, _material.GetFloat("_InnerWidth")));
        float outer = Mathf.Min(limit, Mathf.Max(.001f, _material.GetFloat("_OuterWidth")));
        if (_snapshot == null || _snapshot.MatchId != snapshot.MatchId || _snapshot.GridId != snapshot.GridId ||
            _snapshot.Revision != snapshot.Revision || !ReferenceEquals(_snapshot.Definition, snapshot.Definition) ||
            !Mathf.Approximately(inner, _innerWidth) || !Mathf.Approximately(outer, _outerWidth))
        {
            BoundarySegments = RegionBoundaryGeometry.Build(snapshot);
            RebuildMesh(BoundarySegments, inner, outer, (float)grid.OriginZ);
            _snapshot = snapshot;
            _innerWidth = inner;
            _outerWidth = outer;
        }
        _renderer.enabled = true;
    }

    private bool CreateSurface()
    {
        var source = Resources.Load<Material>("Core/FieldTerritory");
        if (source == null || SortingLayer.NameToID(SortingLayerName) == 0)
        {
            Debug.LogError("Field territory requires Core/FieldTerritory and the Territory Sorting Layer.", this);
            enabled = false;
            return false;
        }
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
        _material = new Material(source) { name = "Field territory (runtime)" };
        _mesh = new Mesh { name = "Captured cell boundary (world XY)" };
        // Canvas 자식으로 만들면 화면 배율이 월드 좌표를 바꾸므로 별도 Scene 루트에 둡니다.
        _surface = new GameObject("Field territory ground");
        SceneManager.MoveGameObjectToScene(_surface, gameObject.scene);
        _surface.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _surface.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.sortingLayerName = SortingLayerName;
        _renderer.sortingOrder = 0;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _snapshot = null;
        return true;
    }

    private void RebuildMesh(IReadOnlyList<RegionBoundarySegment> edges, float inner, float outer, float z)
    {
        var vertices = new List<Vector3>(edges.Count * 8);
        var uv = new List<Vector2>(edges.Count * 8);
        var triangles = new List<int>(edges.Count * 12);
        int first = 0;
        while (first < edges.Count)
        {
            int last = first;
            float length = 0;
            do
            {
                length += Vector2.Distance(edges[last].Start, edges[last].End);
                if (edges[last].End == edges[first].Start) break;
                last++;
            } while (last < edges.Count);

            // 닫힌 경계에 정수 개의 빛을 배치해 시작/끝 연결부에서도 흐름이 끊기지 않습니다.
            float repeats = Mathf.Max(1, Mathf.Round(length / 24));
            float distance = 0;
            for (int i = first; i <= last; i++)
            {
                var edge = edges[i];
                var previous = edges[i == first ? last : i - 1];
                var next = edges[i == last ? first : i + 1];
                Vector2 a = CornerOffset(previous.Inward, edge.Inward);
                Vector2 b = CornerOffset(edge.Inward, next.Inward);
                float endDistance = distance + Vector2.Distance(edge.Start, edge.End);
                float from = distance / length * repeats, to = endDistance / length * repeats;
                AddStrip(edge, a * inner, b * inner, 1, from, to);
                AddStrip(edge, -a * outer, -b * outer, -1, from, to);
                distance = endDistance;
            }
            first = last + 1;
        }
        _mesh.Clear();
        _mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uv);
        _mesh.SetTriangles(triangles, 0);
        _mesh.RecalculateBounds();

        void AddStrip(RegionBoundarySegment edge, Vector2 offsetA, Vector2 offsetB, float side, float from, float to)
        {
            int index = vertices.Count;
            vertices.Add(new Vector3(edge.Start.x, edge.Start.y, z));
            vertices.Add(new Vector3(edge.End.x, edge.End.y, z));
            vertices.Add(new Vector3(edge.End.x + offsetB.x, edge.End.y + offsetB.y, z));
            vertices.Add(new Vector3(edge.Start.x + offsetA.x, edge.Start.y + offsetA.y, z));
            uv.Add(new Vector2(0, from)); uv.Add(new Vector2(0, to));
            uv.Add(new Vector2(side, to)); uv.Add(new Vector2(side, from));
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }
    }

    private static Vector2 CornerOffset(Vector2 incoming, Vector2 outgoing)
    {
        // 직각으로 꺾이는 셀 경계를 연결하며 확정된 중심선의 좌표는 유지합니다.
        return (incoming + outgoing) / Mathf.Max(.001f, 1 + Vector2.Dot(incoming, outgoing));
    }

    private void OnDisable() { if (_renderer != null) _renderer.enabled = false; }
    private void OnDestroy()
    {
        if (_surface != null) Destroy(_surface);
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
    }
}
