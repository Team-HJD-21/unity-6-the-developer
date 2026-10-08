using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public abstract class Monster : MonoBehaviour
{
    [Header("Monster Stats")] public string monsterName;
    public float maxHealth;
    public float currentHealth;
    public float attackDamage;
    public float moveSpeed;
    public float attackRange;
    public float attackCooldown; // Added attackCooldown here
    public bool isDead;
    public bool isTargeted;
    [Header("Detection Settings")] [Tooltip("플레이어를 감지할 범위")]
    public float detectionRange; // 기본값 설정

    [Header("References")] [Tooltip("플레이어 Transform 할당")]
    public Transform player;

    [FormerlySerializedAs("controlUnitStatus")]
    [Tooltip("제어 장치의 ControlUnitHealth를 할당")]
    public ControlUnitHealth controlUnitHealth;

    [Header("UI Elements")] public GameObject healthBarPrefab;
    private Image healthBarCurrentImage; // Current 이미지를 참조
    private Transform healthBarTransform;

    [Header("Health Bar Settings")] [Tooltip("체력바의 Y 위치 오프셋을 설정")]
    public float healthBarYOffset;

    private Animator animator;
    private PolygonCollider2D polygonCollider;

    [Header("For BossMonster")] 
    public bool isDerivedBoss = false;

    protected virtual void Start()
    {

        polygonCollider = GetComponent<PolygonCollider2D>();

        currentHealth = maxHealth;
        isTargeted = false;
        // 체력바 프리팹 인스턴스화
        if (healthBarPrefab != null)
        {
            GameObject healthBarInstance = Instantiate(healthBarPrefab);
            healthBarTransform = healthBarInstance.transform;

            // Current 이미지를 찾아서 참조
            healthBarCurrentImage = healthBarInstance.transform.Find("Current").GetComponent<Image>();

            // 체력바를 몬스터의 자식으로 설정
            healthBarTransform.SetParent(transform, false);

            // 로컬 위치를 조정하여 몬스터 위에 표시
            RectTransform healthBarRect = healthBarTransform.GetComponent<RectTransform>();
            healthBarRect.localPosition = Vector3.zero;
            healthBarRect.anchoredPosition = new Vector2(0, healthBarYOffset);

            // 체력바가 항상 카메라를 향하도록 설정
            healthBarInstance.AddComponent<Billboard>();
        }
        else
        {
            // Debug.LogWarning("Health bar prefab is not assigned on " + monsterName);
        }

        // ControlUnitHealth가 할당되지 않은 경우, 태그를 통해 씬에서 찾아 할당
        if (controlUnitHealth == null)
        {
            GameObject controlUnitObj = GameObject.FindWithTag("CU");
            if (controlUnitObj != null)
            {
                controlUnitHealth = controlUnitObj.GetComponent<ControlUnitHealth>();
                if (controlUnitHealth == null)
                {
                    // Debug.LogWarning("ControlUnitHealth component not found on the ControlUnit object.");
                }
            }
            else
            {
                // Debug.LogWarning("Control Unit not found in the scene. Please assign it manually.");
            }
        }

        // Player가 할당되지 않은 경우, 태그를 통해 씬에서 찾아 할당
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                // Debug.LogWarning("Player not found in the scene. Please assign it manually.");
            }
        }

        // For In Game
        isDead = false;
        // if (!isDerivedBoss)
        // {
        //     GeneralManager.Instance.inGameManager.ListenMonsterSpawn();
        // }
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healthBarCurrentImage != null)
        {
            float fillAmount = currentHealth / maxHealth;
            healthBarCurrentImage.fillAmount = fillAmount;
        }

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            Die();
        }
    }

    public void SetMonsterDerivedBoss(bool flag)
    {
        isDerivedBoss = flag;
    }

    protected void Die()
    {

        //  For Debug
        if (GeneralManager.Instance.inGameManager != null)
        {
            if (!isDerivedBoss)
            {
                GeneralManager.Instance.inGameManager.ListenMonsterDie();
            }
        }
        
        // 체력바 제거
        if (healthBarTransform != null)
        {
            Destroy(healthBarTransform.gameObject);
        }
        
        if (polygonCollider != null)
        {
            Destroy(polygonCollider);
        }
        
        moveSpeed = 0;

        Animator[] animators = GetComponentsInChildren<Animator>();
        foreach (Animator anim in animators)
        {
            anim.enabled = false;
        }
        
        
        StartCoroutine(FadeOutAndDestroy(1.0f));
    }

    private IEnumerator FadeOutAndDestroy(float duration)
    {
        // Get all renderers in the monster and its children
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        // Store the original colors
        Color[][] originalColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            int materialCount = renderers[i].materials.Length;
            originalColors[i] = new Color[materialCount];
            for (int j = 0; j < materialCount; j++)
            {
                originalColors[i][j] = renderers[i].materials[j].color;
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

            for (int i = 0; i < renderers.Length; i++)
            {
                for (int j = 0; j < renderers[i].materials.Length; j++)
                {
                    Color color = originalColors[i][j];
                    color.a = alpha;
                    renderers[i].materials[j].color = color;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure alpha is set to 0
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int j = 0; j < renderers[i].materials.Length; j++)
            {
                Color color = originalColors[i][j];
                color.a = 0f;
                renderers[i].materials[j].color = color;
            }
        }

        // Finally, destroy the monster game object
        Destroy(gameObject);
    }

    protected virtual void Move(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        Vector3 movement = direction * (moveSpeed * Time.deltaTime);
        transform.position += movement;
    }

    protected virtual void Attack()
    {
        // 기본 공격 로직
    }
}
