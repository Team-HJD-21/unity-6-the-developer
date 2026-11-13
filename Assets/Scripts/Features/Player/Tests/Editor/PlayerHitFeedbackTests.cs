using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerHitFeedbackTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _hud, _actor, _cameraObject;
    private PlayerInfo _health;
    private StagePlayerVitalsView _vitals;
    private PlayerHitFeedback _feedback;
    private CanvasGroup _overlay;
    private Camera _camera;

    [SetUp]
    public void SetUp()
    {
        _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab"));
        _feedback = _hud.GetComponent<PlayerHitFeedback>();
        Assert.That(_feedback, Is.Not.Null);
        _vitals = _hud.GetComponent<StagePlayerVitalsView>();
        _overlay = _hud.GetComponentInChildren<PlayerDamageVignette>(true).GetComponent<CanvasGroup>();
        _actor = new GameObject("Feedback local actor");
        _health = _actor.AddComponent<PlayerInfo>(); Call(_health, "Awake");
        Set(_vitals, "_player", _health);
        Call(_feedback, "OnEnable");
        _cameraObject = new GameObject("Feedback camera");
        _camera = _cameraObject.AddComponent<Camera>(); _camera.enabled = false;
        Set(_feedback, "_camera", _camera);
    }

    [TearDown]
    public void TearDown()
    {
        if (_feedback != null) Call(_feedback, "OnDisable");
        if (_hud != null) Object.DestroyImmediate(_hud);
        if (_actor != null) Object.DestroyImmediate(_actor);
        if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
    }

    [Test]
    public void RealDamageFlashesButHealthRefreshRecoveryAndRejectedDamageDoNot()
    {
        _health.onHpChange.Invoke(100, 400);
        Assert.That(_overlay.alpha, Is.Zero);
        Assert.That(_health.ApplyDamage(0), Is.False);
        Assert.That(_overlay.alpha, Is.Zero);
        Assert.That(_health.ApplyDamage(20), Is.True);
        Assert.That(_overlay.alpha, Is.GreaterThan(0));
        Call(_feedback, "Advance", 1f);
        Assert.That(_overlay.alpha, Is.Zero);
        _health.RecoverHp();
        _health.onHpChange.Invoke(_health.curHp, _health.maxHp);
        Assert.That(_overlay.alpha, Is.Zero);
        Set(_health, "_matchBound", true); Set(_health, "_healthAuthority", false);
        Assert.That(_health.ApplyDamage(20), Is.False);
        Assert.That(_overlay.alpha, Is.Zero);
    }

    [Test]
    public void RepeatedHitsStayBoundedAndFadeWithUnscaledElapsedTime()
    {
        _health.ApplyDamage(20); float peak = _overlay.alpha;
        Call(_feedback, "Advance", .1f);
        Assert.That(_overlay.alpha, Is.LessThan(peak));
        _health.ApplyDamage(20);
        Assert.That(_overlay.alpha, Is.EqualTo(peak).Within(.0001f));
        Assert.That(_overlay.alpha, Is.LessThanOrEqualTo(.65f));
        Call(_feedback, "Advance", 1f);
        Assert.That(_overlay.alpha, Is.Zero);
    }

    [Test]
    public void RenderOffsetsRestoreFollowPoseWithoutDriftAndIgnoreOtherCameras()
    {
        _health.ApplyDamage(20);
        for (int i = 0; i < 3; i++)
        {
            Vector3 position = new Vector3(i, 2, -10); _camera.transform.position = position;
            Call(_feedback, "BeginCamera", _camera);
            Assert.That(Vector3.Distance(position, _camera.transform.position), Is.GreaterThan(.001f));
            Call(_feedback, "EndCamera", _camera);
            Assert.That(_camera.transform.position, Is.EqualTo(position));
        }
        Set(_feedback, "_camera", null);
        var before = _camera.transform.position;
        Call(_feedback, "BeginCamera", _camera);
        Assert.That(_camera.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void DisableMidRenderRestoresCameraAndRemovesDamageSubscription()
    {
        _health.ApplyDamage(10);
        Vector3 position = _camera.transform.position;
        Call(_feedback, "BeginCamera", _camera);
        Call(_feedback, "OnDisable");
        Assert.That(_camera.transform.position, Is.EqualTo(position));
        Assert.That(_overlay.alpha, Is.Zero);
        _health.ApplyDamage(10);
        Assert.That(_overlay.alpha, Is.Zero);
    }

    [Test]
    public void UnboundOrReplacedActorDoesNotFlashTheLocalHud()
    {
        _health.ApplyDamage(10);
        Set(_vitals, "_player", null);
        Call(_feedback, "RefreshBinding");
        Assert.That(_overlay.alpha, Is.Zero);
        _health.ApplyDamage(10);
        Assert.That(_overlay.alpha, Is.Zero);
    }

    [Test]
    public void LethalHitReportsOnlyAppliedHealthBeforeDeath()
    {
        int applied = 0; bool alreadyDead = true;
        _health.Damaged += amount => { applied = amount; alreadyDead = (bool)typeof(PlayerInfo).GetField("_isDead", Private).GetValue(_health); };
        _health.ApplyDamage(1000);
        Assert.That(applied, Is.EqualTo(400));
        Assert.That(alreadyDead, Is.False);
        Assert.That(_overlay.alpha, Is.GreaterThan(0));
        Assert.That(_health.IsDestroyed, Is.True);
        Assert.That(_health.ApplyDamage(1), Is.False);
    }

    [Test]
    public void PrefabVignetteFillsViewportLeavesCenterClearAndDoesNotBlockInput()
    {
        var graphic = _overlay.GetComponent<PlayerDamageVignette>();
        Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
        Assert.That(graphic.raycastTarget || _overlay.blocksRaycasts || _overlay.interactable, Is.False);
        Assert.That(_overlay.ignoreParentGroups, Is.True, "HUD intro fading must not hide damage feedback.");
        Assert.That(graphic.maskable, Is.False, "HUD reveal masks must not clip the screen effect.");
        Assert.That(graphic.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(graphic.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 640);
        graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 360);
        using (var helper = new VertexHelper())
        {
            typeof(PlayerDamageVignette).GetMethod("OnPopulateMesh", Private, null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
            Assert.That(helper.currentVertCount, Is.EqualTo(128));
            Assert.That(helper.currentIndexCount, Is.EqualTo(384));
            var vertex = new UIVertex();
            for (int i = 0; i < helper.currentVertCount; i += 2)
            {
                helper.PopulateUIVertex(ref vertex, i);
                Assert.That(vertex.color.a, Is.Zero, "The inner edge must fade to transparent.");
            }
        }
    }

    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static void Call(object owner, string method, params object[] values) => owner.GetType().GetMethod(method, Private).Invoke(owner, values);
}
