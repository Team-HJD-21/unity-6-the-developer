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
    private static readonly int FourLeaves = Animator.StringToHash("Base Layer.FourLeaves");
    private static readonly int FiveLeaves = Animator.StringToHash("Base Layer.FiveLeaves");
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
        public Animator Animator;
        public float Speed;

        public void Stop()
        {
            Display.enabled = false;
            Animator.enabled = false;
        }
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
        var controller = Resources.Load<RuntimeAnimatorController>("TreeLeaves/TreeLeafFallController");
        var material = Resources.Load<Material>("TreeLeafFallMaterial");
        if (controller == null || material == null)
            return false;
        _random = new System.Random(GetInstanceID());
        _bursts = new Burst[PoolSize];
        for (int i = 0; i < _bursts.Length; i++)
        {
            var root = new GameObject("Tree leaf burst", typeof(SortingGroup));
            root.transform.SetParent(transform, false);
            var leaf = new GameObject("Leaves", typeof(SpriteRenderer), typeof(Animator));
            leaf.transform.SetParent(root.transform, false);
            var display = leaf.GetComponent<SpriteRenderer>();
            display.sharedMaterial = material;
            display.maskInteraction = SpriteMaskInteraction.None;
            display.spriteSortPoint = SpriteSortPoint.Pivot;
            display.enabled = false;
            var animator = leaf.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f; // Tick advances the animation with field simulation time.
            animator.enabled = false;
            var group = root.GetComponent<SortingGroup>();
            group.sortAtRoot = true;
            _bursts[i] = new Burst { Root = root.transform, Display = display, Group = group, Animator = animator };
        }
        return true;
    }

    private bool Play(SpriteRenderer tree, Matrix4x4 pose)
    {
        Vector3 anchor = pose.GetColumn(3);
        if (!Initialize() || !TryReserveTree(anchor))
            return false;

        int index = Array.FindIndex(_bursts, b => !b.Display.enabled);
        if (index < 0)
            index = _next++ % _bursts.Length;
        Burst burst = _bursts[index];
        bool fourLeaves = _random.Next(2) == 0;
        burst.Speed = Mathf.Lerp(.9f, 1.1f, (float)_random.NextDouble());
        burst.Position = anchor + Vector3.down * .002f;
        ApplyTreeAppearance(burst, tree);
        PlaceAtTree(burst, tree.sprite, pose);
        burst.Root.name = fourLeaves ? "Tree leaves (4)" : "Tree leaves (5)";
        burst.Display.enabled = true;
        burst.Animator.enabled = true;
        if (!burst.Animator.isInitialized)
            burst.Animator.Rebind();
        burst.Animator.Play(fourLeaves ? FourLeaves : FiveLeaves, 0, 0f);
        burst.Animator.Update(0f);
        return true;
    }

    private bool TryReserveTree(Vector3 anchor)
    {
        double now = FieldSimulationTime.Seconds(this);
        if (_lastEmission.TryGetValue(anchor, out double previous) && now - previous < 2f)
            return false;
        if (_lastEmission.Count >= 128)
            _lastEmission.Clear();
        _lastEmission[anchor] = now;
        return true;
    }

    private void ApplyTreeAppearance(Burst burst, SpriteRenderer tree)
    {
        var treeGroup = tree.GetComponent<SortingGroup>();
        burst.Group.sortingLayerID = treeGroup != null ? treeGroup.sortingLayerID : tree.sortingLayerID;
        burst.Group.sortingOrder = treeGroup != null ? treeGroup.sortingOrder : tree.sortingOrder;
        burst.Display.sortingLayerID = tree.sortingLayerID;
        burst.Display.sortingOrder = 1;
        burst.Display.color = tree.color;
        burst.Display.flipX = _random.Next(2) == 0;
    }

    private void PlaceAtTree(Burst burst, Sprite tree, Matrix4x4 pose)
    {
        SetWorldPose(burst);
        Vector3 bottom = pose.MultiplyPoint3x4(new Vector3(tree.bounds.center.x, tree.bounds.min.y));
        burst.Display.transform.position = bottom;
        Vector3 scale = pose.lossyScale;
        float height = tree.bounds.size.y * Mathf.Abs(scale.y);
        float width = tree.bounds.size.x * Mathf.Abs(scale.x);
        float size = Mathf.Min(height / (84f / PixelsPerUnit), width / (76f / PixelsPerUnit));
        burst.Display.transform.localScale = Vector3.one * size;
        burst.Display.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 3f, (float)_random.NextDouble()));
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
            SetWorldPose(burst);
            burst.Animator.speed = 1f;
            burst.Animator.Update(seconds * burst.Speed);
            burst.Animator.speed = 0f;
            if (burst.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                burst.Stop();
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
                    burst.Stop();
        _lastEmission.Clear();
    }

    private void OnDisable() => Clear();
}
