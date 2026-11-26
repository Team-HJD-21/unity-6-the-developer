using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>실제 이동 거리와 발밑 타일을 따라 저공비행 먼지·식생 흔들림만 표시합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public sealed class PlayerHoverFieldFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private Sprite _dustSprite;
    [SerializeField] private Sprite[] _groundSprites, _grassSprites, _vegetationSprites;
    [SerializeField] private SpriteRenderer[] _dustDisplays, _vegetationDisplays;
    [SerializeField, Min(.1f)] private float _spacing = .22f;
    [SerializeField, Min(.1f)] private float _swayRadius = 1.9f;
    private PlayerControlState _controls;
    private PlayerLocationState _legacyLocation;
    private PlayerInfo _health;
    private PlayerDirectionalColliders _feet;
    private Tilemap _ground;
    private Tilemap[] _maps;
    private int _scene = -1, _nextDust;
    private float _distance, _scanRemaining, _searchRemaining;
    private Vector3 _previous;
    private bool _hasPrevious;
    private HashSet<Sprite> _groundSet, _grassSet, _vegetationSet;
    private Puff[] _puffs;
    private Sway[] _sways;
    private static readonly Dictionary<(Tilemap, Vector3Int), PlayerHoverFieldFeedback> Owners = new();

    private sealed class Puff
    {
        public Vector3 Position, Velocity;
        public float Age, Life, Width, Height, Angle;
        public Color Color;
    }
    private sealed class Sway
    {
        public Tilemap Map;
        public Vector3Int Cell;
        public Color Color;
        public TileFlags Flags;
        public Matrix4x4 Matrix;
        public Vector3 Pivot;
        public float Age, Angle;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOwners() => Owners.Clear();

    private void Awake()
    {
        _controls = GetComponent<PlayerControlState>(); _legacyLocation = GetComponent<PlayerLocationState>();
        _health = GetComponent<PlayerInfo>(); _feet = GetComponent<PlayerDirectionalColliders>();
        _groundSet = new HashSet<Sprite>(_groundSprites ?? System.Array.Empty<Sprite>());
        _grassSet = new HashSet<Sprite>(_grassSprites ?? System.Array.Empty<Sprite>());
        _vegetationSet = new HashSet<Sprite>(_vegetationSprites ?? System.Array.Empty<Sprite>());
        _puffs = new Puff[_dustDisplays?.Length ?? 0];
        for (int i = 0; i < _puffs.Length; i++) _puffs[i] = new Puff();
        _sways = new Sway[_vegetationDisplays?.Length ?? 0];
        for (int i = 0; i < _sways.Length; i++) _sways[i] = new Sway();
        Clear();
    }

    private void LateUpdate() => Tick(FieldSimulationTime.DeltaTime(this));

    public void Tick(float seconds)
    {
        if (_puffs == null || _sways == null || !float.IsFinite(seconds) || seconds < 0) return;
        Vector3 feet = _feet != null ? (Vector3)_feet.GroundPosition : transform.position;
        feet.z = transform.position.z;
        bool indoors = (_controls != null && _controls.IsInSpaceship) || (_legacyLocation != null && _legacyLocation.IsInSpaceship);
        if (indoors || (_health != null && _health.curHp <= 0)) { Clear(); return; }
        if (!FieldSimulationTime.CanAdvance(this) || seconds <= 0) { _previous = feet; return; }
        if (_scene != gameObject.scene.handle) { Clear(); FindMaps(); }
        _searchRemaining -= seconds;
        if (_ground == null && _searchRemaining <= 0) FindMaps();
        AdvanceEffects(seconds);
        if (!_hasPrevious) { _previous = feet; _hasPrevious = true; return; }
        Vector3 travel = feet - _previous; _previous = feet;
        float length = ((Vector2)travel).magnitude;
        // 포털·Scene 전환·체크포인트 이동에 긴 흔적을 만들지 않습니다.
        if (length > Mathf.Max(3, seconds * 70)) { Clear(); _previous = feet; _hasPrevious = true; return; }
        if ((_controls != null && !_controls.CanMove) || length < .002f || _ground == null) return;
        Vector3 lookup = new Vector3(feet.x, feet.y, _ground.transform.position.z);
        Sprite surface = _ground.GetSprite(_ground.WorldToCell(lookup));
        if (surface == null || !_groundSet.Contains(surface)) { _distance = 0; return; }
        Vector2 direction = ((Vector2)travel).normalized;
        float spacing = Mathf.Max(.1f, _spacing), speed = length / seconds;
        // 이동 경로 위에 등간격으로 연결합니다. 낮은 프레임률에서도 한곳에 뭉쳐 터지지 않습니다.
        int emitted = 0;
        for (float along = spacing - _distance; along <= length && emitted < _puffs.Length; along += spacing, emitted++)
        {
            float behind = length - along;
            EmitDust(feet - (Vector3)direction * behind, direction, _grassSet.Contains(surface), speed, behind / speed);
        }
        _distance = (_distance + length) % spacing;
        _scanRemaining -= seconds;
        if (_scanRemaining <= 0) { _scanRemaining = .1f; DisturbVegetation(feet, travel); }
    }

    private void FindMaps()
    {
        _scene = gameObject.scene.handle; _searchRemaining = 1;
        _ground = PlayerTurretPlacementRules.FindGround(gameObject.scene);
        var maps = new List<Tilemap>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            maps.AddRange(root.GetComponentsInChildren<Tilemap>());
        _maps = maps.ToArray();
    }

    private void EmitDust(Vector3 feet, Vector2 direction, bool grass, float speed, float age)
    {
        if (_puffs.Length == 0 || _dustSprite == null) return;
        int index = _nextDust++ % _puffs.Length;
        var puff = _puffs[index]; var display = _dustDisplays[index];
        if (display == null) return;
        float speedRatio = Mathf.InverseLerp(1f, 8f, speed);
        float side = ((_nextDust % 5) - 2) * .045f;
        Vector3 sideways = new Vector3(-direction.y, direction.x);
        puff.Position = feet - (Vector3)direction * .8f + sideways * side;
        puff.Velocity = -(Vector3)direction * Mathf.Lerp(.4f, 1f, speedRatio) + sideways * side;
        puff.Age = age; puff.Life = .32f;
        puff.Width = Mathf.Lerp(1.25f, 1.9f, speedRatio) * (grass ? 1f : 1.1f);
        puff.Height = grass ? .65f : .75f;
        puff.Angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        puff.Color = grass ? new Color(.90f, .95f, .72f, .66f) : new Color(1f, .94f, .80f, .72f);
        puff.Color.a *= Mathf.Lerp(.55f, 1f, speedRatio);
        puff.Position += puff.Velocity * age;
        display.sprite = _dustSprite; display.enabled = true;
        if (age >= puff.Life) { display.enabled = false; return; }
        if (_body != null)
        {
            var group = _body.GetComponent<SortingGroup>();
            SetSorting(display, _body.sortingLayerID, (group != null ? group.sortingOrder : _body.sortingOrder) - 1);
        }
        DrawPuff(index);
    }

    private void DrawPuff(int index)
    {
        var puff = _puffs[index]; var display = _dustDisplays[index];
        if (display == null || !display.enabled) return;
        float t = puff.Age / puff.Life;
        display.transform.position = puff.Position;
        display.transform.rotation = Quaternion.Euler(0, 0, puff.Angle);
        // 길이 방향으로만 늘어지고 얇아집니다. 착지 먼지처럼 부풀어 오르지 않습니다.
        float width = puff.Width * Mathf.Lerp(.9f, 1.15f, t);
        float height = puff.Height * Mathf.Lerp(1f, .65f, t);
        Vector3 parentScale = display.transform.parent != null ? display.transform.parent.lossyScale : Vector3.one;
        display.transform.localScale = new Vector3(
            width / Mathf.Max(.01f, _dustSprite.bounds.size.x) / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
            height / Mathf.Max(.01f, _dustSprite.bounds.size.y) / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
        var color = puff.Color; color.a *= Mathf.Clamp01(puff.Age / .035f) * (1f - Mathf.Clamp01(t)); display.color = color;
    }

    private void AdvanceEffects(float seconds)
    {
        for (int i = 0; i < _puffs.Length; i++)
        {
            var puff = _puffs[i]; var display = _dustDisplays[i];
            if (display == null || !display.enabled) continue;
            puff.Age += seconds; puff.Position += puff.Velocity * seconds;
            if (puff.Age >= puff.Life) display.enabled = false; else DrawPuff(i);
        }
        for (int i = 0; i < _sways.Length; i++)
        {
            var sway = _sways[i];
            if (sway.Map == null) { Release(i); continue; }
            sway.Age += seconds;
            if (sway.Age >= .65f || _vegetationDisplays[i] == null || sway.Map.GetSprite(sway.Cell) != _vegetationDisplays[i].sprite) { Release(i); continue; }
            float t = sway.Age / .65f;
            float angle = sway.Angle * Mathf.Sin(t * Mathf.PI * 2) * (1 - t);
            Matrix4x4 pose = sway.Map.transform.localToWorldMatrix * sway.Matrix *
                Matrix4x4.Translate(sway.Pivot) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)) * Matrix4x4.Translate(-sway.Pivot);
            var display = _vegetationDisplays[i];
            display.transform.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
            var parent = display.transform.parent;
            Vector3 inherited = parent != null ? parent.lossyScale : Vector3.one;
            Vector3 scale = pose.lossyScale;
            display.transform.localScale = new Vector3(scale.x / inherited.x, scale.y / inherited.y, scale.z / inherited.z);
        }
    }

    private void DisturbVegetation(Vector3 feet, Vector3 travel)
    {
        if (_maps == null) return;
        foreach (var map in _maps)
        {
            if (map == null || !map.isActiveAndEnabled || map == _ground || !map.TryGetComponent<TilemapRenderer>(out var renderer)) continue;
            Vector3 point = new Vector3(feet.x, feet.y, map.transform.position.z);
            Vector3Int min = map.WorldToCell(point - Vector3.one * _swayRadius), max = map.WorldToCell(point + Vector3.one * _swayRadius);
            int z = map.WorldToCell(point).z;
            for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector3Int(x, y, z); var sprite = map.GetSprite(cell);
                if (sprite == null || !_vegetationSet.Contains(sprite) || Owners.ContainsKey((map, cell))) continue;
                Vector3 center = map.GetCellCenterWorld(cell);
                if (((Vector2)(center - feet)).sqrMagnitude > _swayRadius * _swayRadius) continue;
                int slot = System.Array.FindIndex(_sways, sway => sway.Map == null);
                if (slot < 0) return;
                var display = _vegetationDisplays[slot]; if (display == null) continue;
                var sway = _sways[slot]; sway.Map = map; sway.Cell = cell; sway.Color = map.GetColor(cell); sway.Flags = map.GetTileFlags(cell); sway.Age = 0;
                sway.Matrix = Matrix4x4.Translate(map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor)) * map.orientationMatrix * map.GetTransformMatrix(cell);
                sway.Pivot = new Vector3(0, sprite.bounds.min.y);
                sway.Angle = (Mathf.Abs(travel.x) > .01f ? -Mathf.Sign(travel.x) : (center.x >= feet.x ? -1 : 1)) * 4;
                display.sprite = sprite; display.color = sway.Color * map.color; display.sharedMaterial = renderer.sharedMaterial;
                SetSorting(display, renderer.sortingLayerID, renderer.sortingOrder); display.spriteSortPoint = SpriteSortPoint.Pivot;
                Owners[(map, cell)] = this;
                map.SetTileFlags(cell, sway.Flags & ~TileFlags.LockColor);
                var hidden = sway.Color; hidden.a = 0; map.SetColor(cell, hidden);
                display.enabled = true;
            }
        }
        AdvanceEffects(0);
    }

    private void Release(int index)
    {
        var sway = _sways[index];
        if (!ReferenceEquals(sway.Map, null) && Owners.TryGetValue((sway.Map, sway.Cell), out var owner) && owner == this)
        {
            if (sway.Map != null && _vegetationDisplays[index] != null && sway.Map.GetSprite(sway.Cell) == _vegetationDisplays[index].sprite)
            {
                var hidden = sway.Color; hidden.a = 0;
                if (sway.Map.GetColor(sway.Cell) == hidden) sway.Map.SetColor(sway.Cell, sway.Color);
                if (sway.Map.GetTileFlags(sway.Cell) == (sway.Flags & ~TileFlags.LockColor)) sway.Map.SetTileFlags(sway.Cell, sway.Flags);
            }
            Owners.Remove((sway.Map, sway.Cell));
        }
        sway.Map = null;
        if (_vegetationDisplays[index] != null) _vegetationDisplays[index].enabled = false;
    }

    private void Clear()
    {
        if (_sways != null) for (int i = 0; i < _sways.Length; i++) Release(i);
        if (_dustDisplays != null) foreach (var display in _dustDisplays) if (display != null) display.enabled = false;
        _hasPrevious = false; _distance = 0; _scanRemaining = 0;
    }
    private static void SetSorting(SpriteRenderer display, int layer, int order)
    {
        display.sortingLayerID = layer; display.sortingOrder = order;
        if (display.TryGetComponent<SortingGroup>(out var group)) { group.sortingLayerID = layer; group.sortingOrder = order; }
    }
    private void OnDisable() => Clear();
}
