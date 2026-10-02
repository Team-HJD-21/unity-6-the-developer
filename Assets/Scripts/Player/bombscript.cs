using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class bombscript : MonoBehaviour
{
    [SerializeField] private GameObject bomb;
    // [SerializeField] private GameObject effect;
    public float bombDamage = 250f;
    // [SerializeField] private Rigidbody2D rb;
    private float explosionRange = 3f;
    [SerializeField] private Animator animator;  // Animator 컴포넌트 참조
    [FormerlySerializedAs("isbam")] public bool isBomb = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetBool("isboom", false);
        bomb.SetActive(true);  // bomb 오브젝트 활성화
        // effect.SetActive(false);  // effect 오브젝트 비활성화
        StartCoroutine(ActivateBombSequence());  // bomb 활성화 후 비활성화 및 effect 활성화 관리
    }

    IEnumerator ActivateBombSequence()
    {
        // 2.1초 뒤 폭발
        yield return new WaitForSeconds(2.1f);
        animator.SetBool("isboom", true);  // isboom 파라미터를 true로 설정

        // isbomb 파라미터를 true로 설정

        // Core 오디오 서비스로 교체되기 전까지 Legacy AudioManager의 암묵적 생성을 막습니다.
        // AudioManager.Instance.PlaySfx(AudioManager.Sfx.PlayerMine);

        // yield return new WaitForSeconds(0.6f);
        Collider2D[] monsters = Physics2D.OverlapCircleAll(GetComponent<Rigidbody2D>().position, explosionRange);

        // 다중 콜라이더로 인한 단일 적 중복 피격 방지
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();
        int damageInt = Mathf.RoundToInt(bombDamage);

        foreach (var monsterCol in monsters)
        {
            if (monsterCol.CompareTag("Enemy") || monsterCol.transform.root.CompareTag("Enemy"))
            {
                GameObject rootObj = monsterCol.transform.root.gameObject;
                if (damagedRoots.Contains(rootObj))
                    continue;

                damagedRoots.Add(rootObj);

                bool damageApplied = false;

                // 1. NGO 서버 권한 체력 컴포넌트(EnemyHealth) 우선 타격
                EnemyHealth enemyHealth = monsterCol.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damageInt);
                    damageApplied = true;
                }

                // 2. 레거시 Monster 컴포넌트 폴백
                Monster monster = monsterCol.GetComponentInParent<Monster>();
                if (monster != null && !damageApplied)
                {
                    monster.TakeDamage(bombDamage);
                }
            }
        }

        yield return new WaitForSeconds(0.8f);

        Destroy(gameObject);
        // bomb.SetActive(false);  // bomb 오브젝트 숨김
        // effect.SetActive(true);  // effect 활성화

        // yield return new WaitForSeconds(2f);  // 2초 대기
        // effect.SetActive(false);
        // Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (isBomb == true)
        {
            isBomb = false;  // 파라미터를 한 번만 설정하고 이후로는 반복하지 않도록
        }
    }
}