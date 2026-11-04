using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
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
    private static string pendingUnloadSceneName;
    private static AsyncOperation pendingUnloadOperation;

    private Animator _animator;
    private BoxCollider2D _blockingCollider;

    internal static AsyncOperation GetPendingUnload(string sceneName)
    {
        return pendingUnloadSceneName == sceneName && pendingUnloadOperation != null && !pendingUnloadOperation.isDone
            ? pendingUnloadOperation
            : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingUnload()
    {
        pendingUnloadSceneName = null;
        pendingUnloadOperation = null;
    }
    
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _blockingCollider = GetComponent<BoxCollider2D>();
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        _animator.SetBool("nearPlayer", true);
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        _animator.SetBool("nearPlayer", false);
    }
    
    private void TryExit(Collider2D other)
    {
        
        Transform player = other.transform;

        Scene returnScene = SceneManager.GetSceneByName(returnSceneName);
        if (!returnScene.isLoaded)
        {
            Debug.LogError($"Return scene '{returnSceneName}' is not loaded.", this);
            isTransitioning = false;
            return;
        }

        Transform returnTrigger = FindTransformInScene(returnScene, returnTriggerName);
        if (returnTrigger == null)
        {
            Debug.LogError(
                $"Could not find '{returnTriggerName}' in scene '{returnSceneName}'.",
                this
            );
            isTransitioning = false;
            return;
        }

        Vector3 destination = GetSafeReturnPosition(returnTrigger, player);
        MovePlayer(player, destination);
        player.GetComponentInParent<PlayerLocationState>()?.CheckLocation(false);

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(originName);
        if (unloadOperation == null)
        {
            Debug.LogError($"Failed to start unloading scene '{originName}'.", this);
            isTransitioning = false;
            return;
        }

        pendingUnloadSceneName = originName;
        pendingUnloadOperation = unloadOperation;
        unloadOperation.completed += completedOperation =>
        {
            if (pendingUnloadOperation != completedOperation)
                return;

            pendingUnloadSceneName = null;
            pendingUnloadOperation = null;
        };
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
    // 애니메이션 이벤트로 처리할 함수들
    public void ActivateDoorCollider(int active)
    {
        _blockingCollider.enabled = active == 0;
    }

    // 문이 열리고 BoxCollider가 활성화 된 후 충돌
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTransitioning || GetPendingUnload(originName) != null ||
            collision.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        isTransitioning = true;
        // 이동
        TryExit(collision.collider);
    }
}
