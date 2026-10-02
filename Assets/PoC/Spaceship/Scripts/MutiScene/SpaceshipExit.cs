using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceshipExit : MonoBehaviour
{
    [SerializeField]
    private string returnSceneName;
    [SerializeField]
    private string originName;
    
    [SerializeField]
    private string returnTriggerName = "Enter";
    

    [SerializeField]
    private float returnPadding = 0.5f;

    private bool isTransitioning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryExit(other);
    }
    
    private void TryExit(Collider2D other)
    {
        if(other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        
        Transform player = other.transform;

        Scene returnScene = SceneManager.GetSceneByName(returnSceneName);
        if (!returnScene.isLoaded)
        {
            Debug.LogError($"Return scene '{returnSceneName}' is not loaded.", this);
            return;
        }

        Transform returnTrigger = FindTransformInScene(returnScene, returnTriggerName);
        if (returnTrigger == null)
        {
            Debug.LogError(
                $"Could not find '{returnTriggerName}' in scene '{returnSceneName}'.",
                this
            );
            return;
        }

        isTransitioning = true;
        Vector3 destination = GetSafeReturnPosition(returnTrigger, player);
        MovePlayer(player, destination);
        isTransitioning = false;
        
        SceneManager.UnloadSceneAsync(originName);
    }

    private Vector3 GetSafeReturnPosition(Transform returnTrigger, Transform player)
    {
        Collider2D triggerCollider = returnTrigger.GetComponent<Collider2D>();
        Collider2D playerCollider = player.gameObject.GetComponent<Collider2D>();

        if (triggerCollider == null)
        {
            return returnTrigger.position;
        }

        float playerExtent = playerCollider == null ? 0f : playerCollider.bounds.extents.y;
        return new Vector3(
            triggerCollider.bounds.center.x,
            triggerCollider.bounds.min.y - playerExtent - returnPadding,
            player.transform.position.z
        );
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
