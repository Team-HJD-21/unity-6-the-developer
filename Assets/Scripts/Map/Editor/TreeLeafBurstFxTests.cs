using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class TreeLeafBurstFxTests
{
    private Scene _scene;
    private GameObject _owner, _tree;
    private TreeLeafBurstFx _effect;
    private SpriteRenderer _renderer;
    private Texture2D _texture;
    private Sprite _sprite;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _owner = new GameObject("Leaf effect owner");
        SceneManager.MoveGameObjectToScene(_owner, _scene);
        _effect = _owner.AddComponent<TreeLeafBurstFx>();
        _tree = new GameObject("Tree", typeof(SpriteRenderer));
        SceneManager.MoveGameObjectToScene(_tree, _scene);
        _texture = new Texture2D(48, 80);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 48, 80), new Vector2(.5f, .2f), 16f);
        _renderer = _tree.GetComponent<SpriteRenderer>();
        _renderer.sprite = _sprite;
        _renderer.sortingLayerName = "Entities";
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_owner);
        Object.DestroyImmediate(_tree);
        Object.DestroyImmediate(_sprite);
        Object.DestroyImmediate(_texture);
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    private SpriteRenderer[] Active() => _owner.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();

    [Test]
    public void ImportedLeafFramesMatchBothAnimationClips()
    {
        var frames = Resources.LoadAll<Sprite>("TreeLeafFall").ToDictionary(sprite => sprite.name);
        Assert.That(frames.Count, Is.EqualTo(74));
        for (int i = 0; i < 74; i++)
        {
            Sprite sprite = frames["TreeLeafFall_" + i];
            Assert.That(sprite.rect, Is.EqualTo(new Rect(i % 9 * 96, (8 - i / 9) * 96, 96, 96)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48f, 12f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
        }
    }

    [TestCase("FourLeaves", 0)]
    [TestCase("FiveLeaves", 37)]
    public void ControllerStatePlaysItsSpriteSequenceOnce(string stateName, int firstFrame)
    {
        var controller = Resources.Load<AnimatorController>("TreeLeaves/TreeLeafFallController");
        Assert.That(controller, Is.Not.Null);
        AnimatorState state = controller.layers.Single().stateMachine.states
            .Select(child => child.state).Single(candidate => candidate.name == stateName);
        var clip = state.motion as AnimationClip;
        Assert.That(clip, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(clip), Does.EndWith(".anim"));
        Assert.That(clip.frameRate, Is.EqualTo(15f));
        Assert.That(clip.length, Is.EqualTo(37f / 15f).Within(.0001f));
        Assert.That(clip.isLooping, Is.False);
        Assert.That(state.transitions, Is.Empty);

        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        Assert.That(binding.path, Is.Empty);
        Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(keys.Length, Is.InRange(37, 38));
        var expected = Resources.LoadAll<Sprite>("TreeLeafFall").ToDictionary(sprite => sprite.name);
        for (int i = 0; i < keys.Length; i++)
        {
            int frame = firstFrame + Math.Min(i, 36);
            Assert.That(keys[i].time, Is.EqualTo(i / 15f).Within(.0001f));
            Assert.That(keys[i].value, Is.SameAs(expected["TreeLeafFall_" + frame]));
        }
    }

    [Test]
    public void ShakeEmitsFourOrFiveLeavesOnceAndThenFinishes()
    {
        Assert.That(TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.identity), Is.True);
        Assert.That(Active().Length, Is.EqualTo(1));
        Assert.That(Active()[0].transform.parent.name, Is.EqualTo("Tree leaves (4)").Or.EqualTo("Tree leaves (5)"));
        Assert.That(TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.identity), Is.False);
        Sprite initial = Active()[0].sprite;
        string expectedFirst = Active()[0].transform.parent.name == "Tree leaves (4)" ? "TreeLeafFall_0" : "TreeLeafFall_37";
        Assert.That(initial.name, Is.EqualTo(expectedFirst));
        _effect.Tick(.2f);
        Assert.That(Active()[0].sprite, Is.Not.SameAs(initial));
        _effect.Tick(4f);
        Assert.That(Active(), Is.Empty);
    }

    [Test]
    public void LeavesStayAtTheirTreeWhenPlayerMovesAndKeepIndependentSorting()
    {
        Matrix4x4 pose = Matrix4x4.TRS(new Vector3(5f, 8f), Quaternion.identity, Vector3.one);
        TreeLeafBurstFx.Emit(_effect, _renderer, pose);
        SpriteRenderer display = Active().Single();
        Vector3 position = display.transform.position;
        _owner.transform.position = new Vector3(30f, -15f);
        _effect.Tick(.1f);
        Assert.That(Vector3.Distance(display.transform.position, position), Is.LessThan(.001f));
        var group = display.GetComponentInParent<SortingGroup>();
        Assert.That(group.sortAtRoot, Is.True);
        Assert.That(group.sortingLayerID, Is.EqualTo(_renderer.sortingLayerID));
        Assert.That(group.sortingOrder, Is.EqualTo(_renderer.sortingOrder));
    }

    [Test]
    public void RepeatedShakesReuseBoundedPoolAndClearDisablesEveryBurst()
    {
        for (int i = 0; i < 40; i++)
            TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.Translate(Vector3.right * i));
        Assert.That(_owner.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(16));
        Assert.That(Active().Length, Is.EqualTo(16));
        _effect.Clear();
        Assert.That(Active(), Is.Empty);
    }

    [Test]
    public void PauseFreezesLeavesAndPreventsNewBursts()
    {
        var clock = new Clock();
        FieldSimulationActor.Attach(_owner, clock);
        TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.identity);
        _effect.Tick(.2f);
        Sprite frame = Active().Single().sprite;
        clock.Paused = true;
        _effect.Tick(2f);
        Assert.That(Active().Single().sprite, Is.SameAs(frame));
        Assert.That(TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.Translate(Vector3.right * 2f)), Is.False);
        clock.Paused = false;
        _effect.Tick(.2f);
        Assert.That(Active().Single().sprite, Is.Not.SameAs(frame));
    }

    internal sealed class Clock : IMatchTime
    {
        public bool Paused;
        public bool IsAvailable => true;
        public double ElapsedSeconds => 0;
        private readonly Guid _epoch = Guid.NewGuid();
        public MatchTimeSnapshot Snapshot => new(new MatchId("tree-leaf-test"), _epoch, 0, 0, 0, Paused ? 0 : 1, Paused, true);
    }
}

public sealed class TreeLeafBurstFxPlaybackTests
{
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AnimatorAdvancesAndFreezesWithTheMatchClock()
    {
        yield return new EnterPlayMode();
        var owner = new GameObject("Leaf playback test owner");
        var tree = new GameObject("Leaf playback test tree", typeof(SpriteRenderer));
        try
        {
            var effect = owner.AddComponent<TreeLeafBurstFx>();
            var clock = new TreeLeafBurstFxTests.Clock();
            FieldSimulationActor.Attach(owner, clock);
            var renderer = tree.GetComponent<SpriteRenderer>();
            renderer.sprite = Resources.LoadAll<Sprite>("TreeLeafFall").First();
            Assert.That(TreeLeafBurstFx.Emit(effect, renderer, Matrix4x4.identity), Is.True);
            var display = owner.GetComponentsInChildren<SpriteRenderer>().Single(item => item.enabled);
            Assert.That(display.GetComponent<Animator>(), Is.Not.Null);

            Sprite pausedFrame = display.sprite;
            Assert.That(pausedFrame, Is.Not.Null);
            clock.Paused = true;
            double deadline = Time.realtimeSinceStartupAsDouble + .25;
            do
            {
                yield return null;
                Assert.That(display.enabled, Is.True);
                Assert.That(display.sprite, Is.SameAs(pausedFrame));
            } while (Time.realtimeSinceStartupAsDouble < deadline);

            clock.Paused = false;
            deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (display.sprite == pausedFrame && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(display.enabled, Is.True);
            Assert.That(display.sprite, Is.Not.SameAs(pausedFrame));
        }
        finally
        {
            Object.Destroy(owner);
            Object.Destroy(tree);
        }
    }
}
