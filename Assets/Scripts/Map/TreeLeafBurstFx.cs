using System;
using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, DefaultExecutionOrder(350)]
public sealed class TreeLeafBurstFx : MonoBehaviour
{
    private const int PoolSize = 16;
    private const float PixelsPerUnit = 16f;
    private static Atlas _atlas;
    private readonly Dictionary<Vector3, double> _lastEmission = new();
    private Burst[] _bursts;
    private System.Random _random;
    private Behaviour _source;
    private int _next;

    private sealed class Burst
    {
        public Transform Root;
        public SpriteRenderer Display;
        public SortingGroup Group;
        public Vector3 Position;
        public float Age, Speed;
        public Clip Clip;
    }

    public static bool Emit(Behaviour owner, SpriteRenderer tree, Matrix4x4 pose)
    {
        if (owner == null || !owner.isActiveAndEnabled || tree == null || tree.sprite == null || !FieldSimulationTime.CanAdvance(owner))
            return false;
        var effect = owner.GetComponent<TreeLeafBurstFx>();
        if (effect == null)
            effect = owner.gameObject.AddComponent<TreeLeafBurstFx>();
        effect._source = owner;
        return effect.Play(tree, pose);
    }

    private bool Initialize()
    {
        if (_bursts != null)
            return true;
        _atlas ??= Atlas.Load();
        if (_atlas == null)
            return false;
        _random = new System.Random(GetInstanceID());
        _bursts = new Burst[PoolSize];
        for (int i = 0; i < _bursts.Length; i++)
        {
            var root = new GameObject("Tree leaf burst", typeof(SortingGroup));
            root.transform.SetParent(transform, false);
            var leaf = new GameObject("Leaves", typeof(SpriteRenderer));
            leaf.transform.SetParent(root.transform, false);
            var display = leaf.GetComponent<SpriteRenderer>();
            display.sharedMaterial = _atlas.Material;
            display.maskInteraction = SpriteMaskInteraction.None;
            display.spriteSortPoint = SpriteSortPoint.Pivot;
            display.enabled = false;
            var group = root.GetComponent<SortingGroup>();
            group.sortAtRoot = true;
            _bursts[i] = new Burst { Root = root.transform, Display = display, Group = group };
        }
        return true;
    }

    private bool Play(SpriteRenderer tree, Matrix4x4 pose)
    {
        if (!Initialize())
            return false;
        Vector3 anchor = pose.GetColumn(3);
        double now = FieldSimulationTime.Seconds(this);
        if (_lastEmission.TryGetValue(anchor, out double previous) && now - previous < 2f)
            return false;
        if (_lastEmission.Count >= 128)
            _lastEmission.Clear();
        _lastEmission[anchor] = now;

        int index = Array.FindIndex(_bursts, b => !b.Display.enabled);
        if (index < 0)
            index = _next++ % _bursts.Length;
        var burst = _bursts[index];
        burst.Clip = _atlas.Clips[_random.Next(2)];
        burst.Age = 0f;
        burst.Speed = Mathf.Lerp(.9f, 1.1f, (float)_random.NextDouble());
        burst.Position = anchor + Vector3.down * .002f;
        var treeGroup = tree.GetComponent<SortingGroup>();
        burst.Group.sortingLayerID = treeGroup != null ? treeGroup.sortingLayerID : tree.sortingLayerID;
        burst.Group.sortingOrder = treeGroup != null ? treeGroup.sortingOrder : tree.sortingOrder;
        burst.Display.sortingLayerID = tree.sortingLayerID;
        burst.Display.sortingOrder = 1;
        burst.Display.color = tree.color;
        burst.Display.flipX = _random.Next(2) == 0;
        burst.Root.name = burst.Clip.name == "four" ? "Tree leaves (4)" : "Tree leaves (5)";
        SetWorldPose(burst);
        Vector3 bottom = pose.MultiplyPoint3x4(new Vector3(tree.sprite.bounds.center.x, tree.sprite.bounds.min.y));
        burst.Display.transform.position = bottom;
        Vector3 scale = pose.lossyScale;
        float height = tree.sprite.bounds.size.y * Mathf.Abs(scale.y);
        float width = tree.sprite.bounds.size.x * Mathf.Abs(scale.x);
        float size = Mathf.Min(height / (84f / PixelsPerUnit), width / (76f / PixelsPerUnit));
        burst.Display.transform.localScale = Vector3.one * size;
        burst.Display.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 3f, (float)_random.NextDouble()));
        burst.Display.sprite = _atlas.GetSprite(burst.Clip.from);
        burst.Display.enabled = true;
        return true;
    }

    private void LateUpdate()
    {
        if (_source == null || !_source.isActiveAndEnabled)
        {
            Clear();
            return;
        }
        Tick(FieldSimulationTime.DeltaTime(this));
    }

    public void Tick(float seconds)
    {
        if (_bursts == null || !float.IsFinite(seconds) || seconds <= 0f || !FieldSimulationTime.CanAdvance(this))
            return;
        foreach (Burst burst in _bursts)
        {
            if (!burst.Display.enabled)
                continue;
            burst.Age += seconds * burst.Speed;
            int frame = Mathf.FloorToInt(burst.Age * _atlas.Fps);
            if (frame > burst.Clip.to - burst.Clip.from)
            {
                burst.Display.enabled = false;
                continue;
            }
            SetWorldPose(burst);
            burst.Display.sprite = _atlas.GetSprite(burst.Clip.from + frame);
        }
    }

    private void SetWorldPose(Burst burst)
    {
        burst.Root.SetPositionAndRotation(burst.Position, Quaternion.identity);
        Vector3 inherited = transform.lossyScale;
        burst.Root.localScale = new Vector3(
            Mathf.Abs(inherited.x) > .001f ? 1f / inherited.x : 1f,
            Mathf.Abs(inherited.y) > .001f ? 1f / inherited.y : 1f,
            Mathf.Abs(inherited.z) > .001f ? 1f / inherited.z : 1f);
    }

    public void Clear()
    {
        if (_bursts != null)
            foreach (Burst burst in _bursts)
                if (burst.Display != null)
                    burst.Display.enabled = false;
        _lastEmission.Clear();
    }

    private void OnDisable() => Clear();

    private sealed class Atlas
    {
        public Material Material;
        public Clip[] Clips;
        public int Fps;
        private Texture2D _texture;
        private AtlasData _data;
        private Sprite[] _sprites;

        public static Atlas Load()
        {
            var texture = Resources.Load<Texture2D>("TreeLeafFall");
            var frames = Resources.Load<TextAsset>("TreeLeafFallFrames");
            var material = Resources.Load<Material>("TreeLeafFallMaterial");
            if (texture == null || frames == null || material == null)
                return null;
            var data = JsonUtility.FromJson<AtlasData>(frames.text);
            return new Atlas
            {
                _texture = texture, _data = data, _sprites = new Sprite[data.frames.Length],
                Material = material, Clips = data.meta.frameTags, Fps = data.meta.pixelfx.fps
            };
        }

        public Sprite GetSprite(int index)
        {
            if (_sprites[index] != null)
                return _sprites[index];
            var rect = _data.frames[index].frame;
            int[] pivot = _data.meta.pixelfx.pivot;
            var sprite = Sprite.Create(_texture,
                new Rect(rect.x, _texture.height - rect.y - rect.h, rect.w, rect.h),
                new Vector2((float)pivot[0] / rect.w, 1f - (float)pivot[1] / rect.h),
                PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = _data.frames[index].filename;
            sprite.hideFlags = HideFlags.DontSave;
            return _sprites[index] = sprite;
        }
    }

    [Serializable] private sealed class AtlasData { public Frame[] frames; public Meta meta; }
    [Serializable] private sealed class Meta { public Clip[] frameTags; public Settings pixelfx; }
    [Serializable] private sealed class Settings { public int fps; public int[] pivot; }
    [Serializable] private sealed class Frame { public string filename; public FrameRect frame; }
    [Serializable] private sealed class FrameRect { public int x, y, w, h; }
    [Serializable] private sealed class Clip { public string name; public int from, to; }
}
