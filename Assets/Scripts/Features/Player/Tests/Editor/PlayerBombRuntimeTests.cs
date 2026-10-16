using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerBombRuntimeTests
{
    private GameObject _bomb;
    private PlayerBombExplosion _explosion;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _bomb = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/bomb.prefab"),
            new Vector3(12000, 12000, 0), Quaternion.identity);
        _explosion = _bomb.GetComponent<PlayerBombExplosion>();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_bomb != null) Object.DestroyImmediate(_bomb);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator Detonation_HidesBodyAndTimer()
    {
        Assert.That(_bomb.transform.Find("Live Timer").gameObject.activeSelf, Is.True);
        _explosion.Explode();
        Assert.That(_bomb.GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(_bomb.transform.Find("Live Timer").gameObject.activeSelf, Is.False);
        yield break;
    }
    [UnityTest]
    public IEnumerator Detonation_ActivatesLegacyExplosionWithoutRangeRing()
    {
        _explosion.Explode();
        Assert.That(_bomb.transform.Find("Legacy Explosion").gameObject.activeSelf, Is.True);
        Assert.That(_bomb.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        yield break;
    }
    [UnityTest]
    public IEnumerator Detonation_CannotRepeat()
    {
        Assert.That(_explosion.Explode(), Is.True);
        Assert.That(_explosion.Explode(), Is.False);
        yield break;
    }

    [TestCase(.5f, 6f)]
    [TestCase(2.5f, 9f)]
    [TestCase(5f, 10f)]
    public void Detonation_MatchesDamageRadiusRegardlessOfBodyScale(float bodyScale, float radius)
    {
        _bomb.transform.localScale = new Vector3(bodyScale, bodyScale * .8f, 1f);
        Vector3 originalBodyScale = _bomb.transform.localScale;
        var data = new SerializedObject(_explosion);
        data.FindProperty("_radius").floatValue = radius;
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(_explosion.Explode(), Is.True);
        var visualData = new SerializedObject(_bomb.GetComponent<PlayerTimeBombVisual>());
        float referenceDiameter = visualData.FindProperty("_explosionDiameterAtScaleOne").floatValue;
        Transform effect = _bomb.transform.Find("Legacy Explosion");
        Assert.That(referenceDiameter * Mathf.Abs(effect.lossyScale.x), Is.EqualTo(radius * 2f).Within(.001f));
        Assert.That(referenceDiameter * Mathf.Abs(effect.lossyScale.y), Is.EqualTo(radius * 2f).Within(.001f));
        Assert.That(_bomb.transform.localScale, Is.EqualTo(originalBodyScale));
    }
}
