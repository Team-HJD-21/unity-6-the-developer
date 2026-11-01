// 우주선 입구에서 목적지를 로드하고 Host가 이동을 확정한 뒤 참가자 위치를 보고합니다.
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceshipEnter : MonoBehaviour
{
    [SerializeField]
    private string sceneToLoadName;

    [SerializeField]
    private string destinationName = "Triangle";

    private bool isTransitioning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        
        if (isTransitioning) return;
        var presence = other.GetComponentInParent<PlayerMatchPresence>();
        if (presence != null && !presence.CanReport) return;
        Transform player = other.transform;

        StartCoroutine(EnterSpaceship(player));
    }

    private IEnumerator EnterSpaceship(Transform player)
    {
        isTransitioning = true;

        if (string.IsNullOrWhiteSpace(sceneToLoadName))
        {
            Debug.LogError("Spaceship scene name is not assigned.", this);
            isTransitioning = false;
            yield break;
        }

        Scene spaceshipScene = SceneManager.GetSceneByName(sceneToLoadName);
        if (!spaceshipScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                sceneToLoadName,
                LoadSceneMode.Additive
            );

            if (loadOperation == null)
            {
                Debug.LogError($"Failed to start loading scene '{sceneToLoadName}'.", this);
                isTransitioning = false;
                yield break;
            }

            yield return loadOperation;
            spaceshipScene = SceneManager.GetSceneByName(sceneToLoadName);
        }
        
        // 로드된 상태의 spaceship
        Transform destination = FindTransformInScene(spaceshipScene, destinationName);
        if (destination == null)
        {
            Debug.LogError(
                $"Could not find '{destinationName}' in scene '{sceneToLoadName}'.",
                this
            );
            isTransitioning = false;
            yield break;
        }

        if (player == null) { isTransitioning = false; yield break; }
        var presence = player.GetComponentInParent<PlayerMatchPresence>();
        if (presence != null && !presence.CanReport) { isTransitioning = false; yield break; }
        MovePlayer(player, destination.position);
        presence?.Report(MatchPlayerLocation.Spaceship);
        isTransitioning = false;
    }

    private static Transform FindTransformInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static void MovePlayer(Transform player, Vector3 destination)
    {
        Rigidbody2D body = player.gameObject.GetComponentInParent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = destination;
            return;
        }

        player.transform.position = destination;
    }
}
