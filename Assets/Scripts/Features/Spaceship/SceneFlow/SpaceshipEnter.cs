using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class SpaceshipEnter : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("sceneToLoadName")]
    private string _sceneToLoadName;

    [SerializeField]
    [FormerlySerializedAs("destinationName")]
    private string _destinationName = "Triangle";

    private bool _isTransitioning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isTransitioning || other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        
        Transform player = other.transform;

        StartCoroutine(EnterSpaceship(player));
    }

    private IEnumerator EnterSpaceship(Transform player)
    {
        _isTransitioning = true;

        if (string.IsNullOrWhiteSpace(_sceneToLoadName))
        {
            Debug.LogError("Spaceship scene name is not assigned.", this);
            _isTransitioning = false;
            yield break;
        }

        AsyncOperation pendingUnload = SpaceshipExit.GetPendingUnload(_sceneToLoadName);
        if (pendingUnload != null)
            yield return pendingUnload;

        Scene spaceshipScene = SceneManager.GetSceneByName(_sceneToLoadName);
        if (!spaceshipScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                _sceneToLoadName,
                LoadSceneMode.Additive
            );

            if (loadOperation == null)
            {
                Debug.LogError($"Failed to start loading scene '{_sceneToLoadName}'.", this);
                _isTransitioning = false;
                yield break;
            }

            yield return loadOperation;
            spaceshipScene = SceneManager.GetSceneByName(_sceneToLoadName);
        }
        
        // 로드된 상태의 spaceship
        Transform destination = FindTransformInScene(spaceshipScene, _destinationName);
        if (destination == null)
        {
            Debug.LogError(
                $"Could not find '{_destinationName}' in scene '{_sceneToLoadName}'.",
                this
            );
            _isTransitioning = false;
            yield break;
        }

        MovePlayer(player, destination.position);
        player.GetComponentInParent<PlayerLocationState>()?.CheckLocation(true);
        _isTransitioning = false;
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

    private void MovePlayer(Transform player, Vector3 destination)
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
