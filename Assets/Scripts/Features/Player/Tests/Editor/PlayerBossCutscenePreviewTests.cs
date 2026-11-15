using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerBossCutscenePreviewTests
{
    private Scene _scene;
    private GameObject _actor, _cameraObject, _previewObject;
    private PlayerInfo _health;
    private Camera _camera;
    private PlayerBossCutscenePreview _preview;
    private InputLockService _input;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        yield return null;
        _scene = SceneManager.CreateScene("Boss preview fixture");
        _input = new InputLockService();
        _actor = new GameObject("Preview player", typeof(SpriteRenderer), typeof(PlayerInfo), typeof(PlayerControlState));
        SceneManager.MoveGameObjectToScene(_actor, _scene);
        _actor.transform.position = new Vector3(70000, 70000);
        _actor.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab").GetComponent<SpriteRenderer>().sprite;
        _actor.GetComponent<PlayerControlState>().BindInputBlocker(_input);
        _health = _actor.GetComponent<PlayerInfo>();
        _cameraObject = new GameObject("Preview camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(_cameraObject, _scene);
        _camera = _cameraObject.GetComponent<Camera>(); _camera.orthographic = true; _camera.orthographicSize = 10;
        _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.15f, .24f, .12f);
        _camera.transform.position = _actor.transform.position + Vector3.back * 10;
        _cameraObject.AddComponent<MatchCameraFollow>().Bind(_actor.transform);
        _previewObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerBossCutscenePreview.prefab"));
        SceneManager.MoveGameObjectToScene(_previewObject, _scene);
        _preview = _previewObject.GetComponent<PlayerBossCutscenePreview>(); _preview.BindInputBlocker(_input);
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_previewObject != null) Object.Destroy(_previewObject);
        if (_cameraObject != null) Object.Destroy(_cameraObject);
        if (_actor != null) Object.Destroy(_actor);
        yield return null;
        _input.Dispose();
        yield return SceneManager.UnloadSceneAsync(_scene);
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator AuthoredButtonCapturesWorldAndRestoresViewInputWithoutMovingPlayerOrScene()
    {
        Assert.That(_preview.IsConfigured, Is.True);
        Vector3 position = _actor.transform.position;
        int scenes = SceneManager.sceneCount;
        var button = (Button)typeof(PlayerBossCutscenePreview).GetField("_previewButton", Private).GetValue(_preview);
        button.onClick.Invoke();
        Assert.That(_preview.IsPlaying, Is.True);
        Assert.That(_input.IsBlocked, Is.True);
        Assert.That(_camera.GetComponent<CinemachineBrain>().enabled, Is.True);
        Assert.That(_cameraObject.GetComponent<MatchCameraFollow>().enabled, Is.False);
        Assert.That(_preview.TryPlay(), Is.False, "Double click cannot start a second playback.");
        bool captured = false; float closestSize = 10, maximumCaption = 0, maximumSuction = 0;
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (_preview.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline)
        {
            var raw = (RawImage)typeof(PlayerBossCutscenePreview).GetField("_distortion", Private).GetValue(_preview);
            if (raw.texture != null) { captured = true; Assert.That(raw.texture.width, Is.GreaterThanOrEqualTo(64)); }
            closestSize = Mathf.Min(closestSize, _camera.orthographicSize);
            maximumCaption = Mathf.Max(maximumCaption, (float)typeof(PlayerBossCutscenePreview).GetField("_captionOpacity", Private).GetValue(_preview));
            maximumSuction = Mathf.Max(maximumSuction, (float)typeof(PlayerBossCutscenePreview).GetField("_suction", Private).GetValue(_preview));
            yield return null;
        }
        Assert.That(captured, Is.True, "The actual renderer must produce the suction source frame.");
        Assert.That(closestSize, Is.EqualTo(5.2f).Within(.05), "Cinemachine shots must actually zoom the output camera.");
        Assert.That(maximumCaption, Is.GreaterThan(.95), "Timeline must animate the caption.");
        Assert.That(maximumSuction, Is.GreaterThan(.95), "Timeline must animate the distortion.");
        Assert.That(_preview.IsPlaying, Is.False);
        Assert.That(_input.IsBlocked, Is.False);
        Assert.That(CameraManager.Current.HasOverride, Is.False);
        Assert.That(_camera.GetComponent<CinemachineBrain>().enabled, Is.False);
        Assert.That(_cameraObject.GetComponent<MatchCameraFollow>().enabled, Is.True);
        Assert.That(_camera.orthographicSize, Is.EqualTo(10).Within(.01));
        Assert.That(_actor.transform.position, Is.EqualTo(position));
        Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
        Assert.That(_preview.TryPlay(), Is.True, "The preview remains replayable.");
        _preview.StopPreview();
    }
    [UnityTest]
    public IEnumerator DisableReleasesOnlyItsOwnLeaseAndClearsTransientRenderResources()
    {
        Assert.That(_preview.TryPlay(), Is.True);
        double deadline = Time.realtimeSinceStartupAsDouble + 8;
        while (_preview.Elapsed < 3.5f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        using (var other = _input.Acquire("another screen"))
        {
            _preview.enabled = false;
            Assert.That(_preview.IsPlaying, Is.False);
            Assert.That(_input.Count, Is.EqualTo(1));
            Assert.That(CameraManager.Current.HasOverride, Is.False);
            Assert.That(typeof(PlayerBossCutscenePreview).GetField("_snapshot", Private).GetValue(_preview), Is.Null);
        }
        Assert.That(_input.IsBlocked, Is.False);
    }
    [UnityTest]
    public IEnumerator DamageCancelsPreviewAndBlockedOrHubPlayerCannotStart()
    {
        using (var other = _input.Acquire("existing screen")) Assert.That(_preview.TryPlay(), Is.False);
        Assert.That(_preview.TryPlay(), Is.True);
        Assert.That(_health.ApplyDamage(1), Is.True);
        Assert.That(_preview.IsPlaying, Is.False);
        Assert.That(_input.IsBlocked, Is.False);
        _actor.GetComponent<PlayerControlState>().BindHub(_input);
        Assert.That(_preview.TryPlay(), Is.False);
        yield break;
    }
}
