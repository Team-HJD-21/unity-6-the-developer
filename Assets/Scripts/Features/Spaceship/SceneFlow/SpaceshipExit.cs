// 우주선 출구의 문 연출 뒤 필드로 이동하고 Host의 참가자 위치를 갱신합니다.
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
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

    private Animator _animator;
    private BoxCollider2D _blockingCollider;
    
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
        
        if (isTransitioning) return;
        var presence = other.GetComponentInParent<PlayerMatchPresence>();
        if (presence != null && !presence.CanReport) return;
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
        presence?.Report(MatchPlayerLocation.Field);
        isTransitioning = false;
        
        // SOS의 다른 참가자가 내부에 남아 있을 수 있으므로 출입 때 공용 허브를 내리지 않습니다.
        // Hub Scene 해제는 게임 사이클 소유자가 참가자/Match 수명에 맞춰 수행합니다.
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
        if (collision.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        // 이동
        TryExit(collision.collider);
    }
}
