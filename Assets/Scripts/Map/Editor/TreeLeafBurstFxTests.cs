using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
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
    public void ShakeEmitsFourOrFiveLeavesOnceAndThenFinishes()
    {
        Assert.That(TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.identity), Is.True);
        Assert.That(Active().Length, Is.EqualTo(1));
        Assert.That(Active()[0].transform.parent.name, Is.EqualTo("Tree leaves (4)").Or.EqualTo("Tree leaves (5)"));
        Assert.That(TreeLeafBurstFx.Emit(_effect, _renderer, Matrix4x4.identity), Is.False);
        Sprite initial = Active()[0].sprite;
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

    private sealed class Clock : IMatchTime
    {
        public bool Paused;
        public bool IsAvailable => true;
        public double ElapsedSeconds => 0;
        private readonly Guid _epoch = Guid.NewGuid();
        public MatchTimeSnapshot Snapshot => new(new MatchId("tree-leaf-test"), _epoch, 0, 0, 0, Paused ? 0 : 1, Paused, true);
    }
}
