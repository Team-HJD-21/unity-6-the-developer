using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class SpaceshipReturnDoor : MonoBehaviour
{
    [SerializeField] private string spaceshipSceneName = "SpaceShipGrayBox";
    [SerializeField] private bool roomOnRight;
    [SerializeField, Range(0.01f, 0.5f)] private float blackoutSeconds = 0.25f;

    private static bool isReturning;
    public static bool IsReturning => isReturning;

    private async void OnTriggerEnter2D(Collider2D other)
    {
        // 이동 중 중복 요청과 Player가 아닌 Collider의 접촉을 무시합니다.
        if (isReturning || SpaceshipRoomDoor.IsTransitioning || !IsPlayerBody(other)) return;
        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null) return;

        isReturning = true;
        Scene room = gameObject.scene;
        try
        {
            // 암전 중 우주선을 먼저 로드하고, 복귀 위치를 정한 뒤 현재 방을 언로드합니다.
            await SpaceshipRoomDoor.FadeRoomAsync(async () =>
            {
                await SpaceshipRoomDoor.LoadRoomAsync(spaceshipSceneName);
                Scene spaceship = SceneManager.GetSceneByName(spaceshipSceneName);
                if (!spaceship.isLoaded)
                    throw new InvalidOperationException("우주선 Scene 로드가 완료되지 않았습니다: " + spaceshipSceneName);

                // 출발한 방과 같은 쪽의 우주선 문을 찾습니다.
                string doorName = roomOnRight ? "RightDoor" : "LeftDoor";
                Transform destinationDoor = null;
                foreach (GameObject root in spaceship.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name != doorName) continue;
                        destinationDoor = child;
                        break;
                    }
                    if (destinationDoor != null) break;
                }
                if (destinationDoor == null)
                    throw new InvalidOperationException("우주선 목적지 문을 찾을 수 없습니다: " + doorName);

                // 문의 우주선 안쪽으로 0.3 이동시키고 Y 위치는 유지합니다.
                float arrivalX = destinationDoor.position.x + (roomOnRight ? -0.3f : 0.3f);
                playerBody.position = new Vector2(arrivalX, playerBody.position.y);
                Physics2D.SyncTransforms();
                await SpaceshipRoomDoor.UnLoadRoomAsync(room.name);
            }, blackoutSeconds);
        }
        catch (Exception error)
        {
            if (this != null)
            {
                Debug.LogException(error, this);
            }
        }
        finally
        {
            isReturning = false;
        }
    }

    private static bool IsPlayerBody(Collider2D other) => !other.isTrigger &&
        other.gameObject.layer == LayerMask.NameToLayer("PlayerFeetBox") &&
        other.GetComponentInParent<PlayerActorIdentity>() != null;
}
