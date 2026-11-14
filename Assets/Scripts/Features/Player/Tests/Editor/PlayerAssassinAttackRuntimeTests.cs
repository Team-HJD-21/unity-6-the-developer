using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerAssassinAttackRuntimeTests
{
    private readonly List<GameObject> _objects = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualBlackWolfRevealsAndTakesDirectAttacksButRejectsTurret()
    {
        yield return new EnterPlayMode();
        Vector3 origin = new Vector3(13000, 13000);
        var player = Track(new GameObject("Assassin detection test player"));
        player.transform.position = origin;
        player.AddComponent<Rigidbody2D>().gravityScale = 0;
        player.AddComponent<PlayerInfo>();
        var rangeObject = new GameObject("AssassinDetectionRange");
        rangeObject.SetActive(false); rangeObject.transform.SetParent(player.transform, false);
        rangeObject.layer = LayerMask.NameToLayer("PlayerFeetBox");
        var range = rangeObject.AddComponent<CircleCollider2D>(); range.isTrigger = true; range.radius = 3;
        range.excludeLayers = ~LayerMask.GetMask("EnemyHitBox");
        var detector = rangeObject.AddComponent<PlayerEnemyDetector>();
        var data = new SerializedObject(detector);
        data.FindProperty("_owner").objectReferenceValue = player.transform;
        data.FindProperty("_range").objectReferenceValue = range;
        data.ApplyModifiedPropertiesWithoutUndo(); rangeObject.SetActive(true);
        var wolf = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Assassin/BlackWolf.prefab"), origin + Vector3.right * 1.5f, Quaternion.identity));
        wolf.AddComponent<EnemySquadTester>();
        var health = wolf.GetComponent<EnemyHealth>(); health.Initialize();
        // Keep this fixture stationary; squad registration still runs normally.
        wolf.GetComponent<EnemyNetworkController>().enabled = false;
        yield return null; yield return null;
        Physics2D.SyncTransforms(); detector.RefreshDetection();
        var detection = wolf.GetComponent<EnemyDetection>();
        Assert.That(detection.IsDetected, Is.True, "The actual Trigger HitBox must reveal BlackWolf.");
        Assert.That(typeof(EnemyController).GetField("_squad", Private).GetValue(wolf.GetComponent<EnemyController>()), Is.Not.Null);
        var hitbox = System.Array.Find(wolf.GetComponentsInChildren<Collider2D>(), c => c.gameObject.layer == LayerMask.NameToLayer("EnemyHitBox"));
        Assert.That(hitbox, Is.Not.Null);

        foreach (bool detected in new[] { false, true })
        {
            range.enabled = detected; detector.RefreshDetection();
            Assert.That(detection.IsDetected, Is.EqualTo(detected));
            foreach (string attack in new[] { "bullet", "cryo", "sniper", "spanner", "bomb", "turret" })
            {
                health.Initialize();
                if (attack == "cryo")
                {
                    var spray = player.GetComponent<PlayerCryoSpray>() ?? player.AddComponent<PlayerCryoSpray>();
                    Set(spray, "_vfxPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/CryoSprayVfx.prefab"));
                    Assert.That(spray.ApplyDamage(origin, Vector2.right, 1), Is.True);
                }
                else if (attack == "bullet" || attack == "sniper")
                {
                    var bulletObject = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(attack == "sniper"
                        ? "Assets/Prefabs/Player/SniperBullet.prefab" : "Assets/Prefabs/Player/PlayerBullet.prefab"), origin, Quaternion.identity));
                    var bullet = bulletObject.GetComponent<PlayerBullet>();
                    Assert.That(bullet.Initialize(Color.white, 1, Vector2.right, attack == "cryo"), Is.True);
                    Call(bullet, "ProcessHit", hitbox);
                }
                else if (attack == "spanner")
                {
                    var spannerObject = Track(new GameObject("Source test spanner"));
                    var spanner = spannerObject.AddComponent<PlayerSpannerWeapon>(); spanner.Bind(player.transform, null);
                    Set(spanner, "_damage", 1); Set(spanner, "_swingDirection", Vector2.right);
                    Call(spanner, "ApplyMeleeDamage");
                }
                else if (attack == "bomb") new PlayerBombAreaDamage().Apply(origin + Vector3.right * 1.5f, 1, 1);
                else
                {
                    var projectileObject = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurretProjectile.prefab"), origin, Quaternion.identity));
                    var projectile = projectileObject.GetComponent<PlayerTurretProjectile>();
                    Assert.That(projectile.Initialize(Vector2.right, 1), Is.True);
                    Call(projectile, "ProcessHit", hitbox);
                }
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - (detected && attack != "turret" ? 1 : 0)), attack + ", detected=" + detected);
            }
        }
        wolf.transform.position = origin + Vector3.right * 30;
        Physics2D.SyncTransforms(); detector.RefreshDetection();
        Assert.That(detection.IsDetected, Is.False);
    }

    [UnityTest]
    public IEnumerator ActualHudDamageRendersRedEdgesAndRestoresCameraThenFades()
    {
        yield return new EnterPlayMode();
        var actor = Track(new GameObject("Local feedback actor"));
        var health = actor.AddComponent<PlayerInfo>();
        var hud = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab")));
        var vitals = hud.GetComponent<StagePlayerVitalsView>(); vitals.BindPlayer(health);
        var feedback = hud.GetComponent<PlayerHitFeedback>();
        // Editor/domain reload frames can take hundreds of ms; keep capture timing controlled.
        Set(feedback, "_flashSeconds", 10f); Set(feedback, "_shakeSeconds", 5f);
        var graphic = hud.GetComponentInChildren<PlayerDamageVignette>(true);
        var overlay = graphic.GetComponent<CanvasGroup>();
        var cameraObject = Track(new GameObject("Damage preview camera"));
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(15000, 15000, -10);
        camera.orthographic = true; camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .25f, .18f);
        // Initial_Stage overrides the prefab's disabled legacy root Canvas to enabled.
        // Nested canvases inherit the root render mode, so redirect that root for capture.
        var canvas = hud.GetComponent<Canvas>(); canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        // A HUD panel at a red edge must keep its original color after damage.
        var panel = Track(new GameObject("Foreground HUD canvas", typeof(RectTransform), typeof(Canvas)));
        var panelCanvas = panel.GetComponent<Canvas>();
        panelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        panelCanvas.worldCamera = camera; panelCanvas.planeDistance = .5f; panelCanvas.sortingOrder = 0;
        var imageObject = new GameObject("HUD edge panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        imageObject.transform.SetParent(panel.transform, false);
        var image = imageObject.GetComponent<UnityEngine.UI.Image>(); image.color = Color.blue; image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
        image.rectTransform.pivot = Vector2.zero;
        image.rectTransform.anchoredPosition = new Vector2(32, 32); image.rectTransform.sizeDelta = new Vector2(48, 48);
        var target = new RenderTexture(640, 360, 24);
        var before = new Texture2D(640, 360, TextureFormat.RGBA32, false);
        var after = new Texture2D(640, 360, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            before.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); before.Apply();
            Assert.That(overlay.alpha, Is.Zero);
            Set(feedback, "_camera", camera);
            Assert.That(health.ApplyDamage(40), Is.True);
            Assert.That(overlay.alpha, Is.GreaterThan(0));
            // CanvasGroup inherited alpha is propagated by the native frame update.
            yield return null;
            Set(feedback, "_camera", camera);
            Vector3 pose = camera.transform.position;
            Canvas.ForceUpdateCanvases(); camera.Render();
            Assert.That(camera.transform.position, Is.EqualTo(pose), "Actual camera callbacks must restore its follow pose.");
            RenderTexture.active = target;
            after.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); after.Apply();
            System.IO.Directory.CreateDirectory("Docs/Local/AssassinHitFeedback20261113");
            System.IO.File.WriteAllBytes("Docs/Local/AssassinHitFeedback20261113/hud-normal.png", before.EncodeToPNG());
            System.IO.File.WriteAllBytes("Docs/Local/AssassinHitFeedback20261113/hud-damaged.png", after.EncodeToPNG());
            var mesh = graphic.canvasRenderer.GetMesh();
            string diagnostic = "rect=" + graphic.rectTransform.rect + ", alpha=" + overlay.alpha + ", inherited=" + graphic.canvasRenderer.GetInheritedAlpha() + ", culled=" + graphic.canvasRenderer.cull + ", vertices=" + (mesh != null ? mesh.vertexCount : 0);
            Assert.That(after.GetPixel(4, 4).r, Is.GreaterThan(before.GetPixel(4, 4).r + .03f), "Damage must actually render a red screen edge. " + diagnostic);
            Assert.That(after.GetPixel(320, 180).r, Is.EqualTo(before.GetPixel(320, 180).r).Within(.02f), "The center must remain clear.");
            Assert.That(before.GetPixel(48, 48).b, Is.GreaterThan(.9f), "The foreground HUD panel must be visible.");
            Assert.That(after.GetPixel(48, 48).r, Is.EqualTo(before.GetPixel(48, 48).r).Within(.02f),
                "The red vignette must not tint the HUD above it.");
            Assert.That(after.GetPixel(48, 48).b, Is.EqualTo(before.GetPixel(48, 48).b).Within(.02f));
            Set(feedback, "_flashSeconds", .45f); Set(feedback, "_shakeSeconds", .2f);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(overlay.alpha, Is.Zero);
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(target); Object.DestroyImmediate(before); Object.DestroyImmediate(after);
        }
    }

    private GameObject Track(GameObject item) { _objects.Add(item); return item; }
    private static void Call(object owner, string method, params object[] values) => owner.GetType().GetMethod(method, Private).Invoke(owner, values);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
}
