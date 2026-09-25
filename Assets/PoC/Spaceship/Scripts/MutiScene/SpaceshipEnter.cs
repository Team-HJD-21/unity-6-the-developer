using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceshipEnter : MonoBehaviour
{
    [SerializeField]
    private string sceneToLoadName;

    [SerializeField]
    private string destinationName = "Triangle";
    private string originName = "";

    private bool isTransitioning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        SamplePlayer player = other.GetComponentInParent<SamplePlayer>();
        if (player == null || isTransitioning)
        {
            return;
        }

        StartCoroutine(EnterSpaceship(player));
    }

    private IEnumerator EnterSpaceship(SamplePlayer player)
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

        MovePlayer(player, destination.position);
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

    private static void MovePlayer(SamplePlayer player, Vector3 destination)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
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
