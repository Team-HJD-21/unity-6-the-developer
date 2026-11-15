// 우주선 벽·문·출구의 실제 제작 콜라이더가 Player 발 콜라이더와 접촉하는지 검사합니다.
// 필드의 기존 상호작용 레이어도 확인하며 Scene이나 전투 충돌 설정을 재작성하지 않습니다.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SpaceshipCollisionRegressionTests
{
    [TestCase("Default")]
    [TestCase("PocInteractArea")]
    [TestCase("WorldGeometry")]
    public void PlayerFeet_CanContactWorldAndInteractionLayers(string layerName)
    {
        int feet = LayerMask.NameToLayer("PlayerFeetBox");
        int world = LayerMask.NameToLayer(layerName);
        Assert.That(feet, Is.GreaterThanOrEqualTo(0));
        Assert.That(world, Is.GreaterThanOrEqualTo(0));
        Assert.That(Physics2D.GetIgnoreLayerCollision(feet, world), Is.False,
            "레이어가 차단되면 벽 접촉과 문 Trigger/Collision 콜백이 모두 도달하지 않습니다.");
    }

    [Test]
    public void AuthoredSpaceshipWallsAndExitDoors_SupportTheActualFeetLayer()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Spaceship/SpaceShipGrayBox.unity");
        try
        {
            int feet = LayerMask.NameToLayer("PlayerFeetBox");
            var colliders = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider2D>(true))
                .Where(collider => collider.enabled && collider.gameObject.activeInHierarchy).ToArray();
            Assert.That(colliders, Is.Not.Empty);
            foreach (var collider in colliders)
                Assert.That(Physics2D.GetIgnoreLayerCollision(feet, collider.gameObject.layer), Is.False, collider.name);
            var exits = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpaceshipExit>(true)).ToArray();
            Assert.That(exits, Is.Not.Empty);
            foreach (var exit in exits)
                Assert.That(exit.GetComponent<BoxCollider2D>().isTrigger, Is.False,
                    "현재 출구는 OnCollisionEnter2D를 사용하므로 실제 접촉용 BoxCollider가 필요합니다.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
