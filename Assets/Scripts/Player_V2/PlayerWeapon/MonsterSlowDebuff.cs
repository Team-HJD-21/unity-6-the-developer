using System.Collections;
using UnityEngine;

/// <summary>
/// 냉각 무기(CryoBlaster) 피격 몬스터의 이동 속도를 일시적으로 감속하고 스프라이트 틴트를 적용/복구하는 디버프 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 동적 AddComponent 기반 독립 생명주기 관리:
///    - 대안: 모든 몬스터 프리팹에 슬로우 제어 스크립트를 기본 부착.
///    - 채택 이유: 디버프가 적용되지 않는 일반 몬스터의 컴포넌트 오버헤드를 줄이고, 
///      필요 시점에만 부착되어 동작 후 자멸(Destroy)하도록 설계하여 리소스를 최적화했습니다.
/// 
/// 2. 원본 색상 캐싱 플래그(_hasOriginalColor):
///    - 대안: 단순 SpriteRenderer.color 저장.
///    - 채택 이유: 디버프 지속 시간 중 추가 피격을 받아 코루틴이 갱신될 때, 
///      하늘색 상태가 '원본 색상'으로 잘못 덮어씌워져 색상이 굳는 현상을 원천 방지합니다.
/// </summary>
public class MonsterSlowDebuff : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool hasOriginalColor = false;
    private Coroutine debuffCoroutine;

    private WalkAndAttackMonster monsterMovement;
    private float originalSpeed = -1f;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        monsterMovement = GetComponent<WalkAndAttackMonster>();
    }

    /// <summary>
    /// PlayerBullet 규격 대응 메서드: 대상 몬스터 참조와 감속 비율, 지속 시간을 주입받아 디버프 개시
    /// </summary>
    public void ApplySlow(Monster targetMonster, float slowRatio, float duration, Color? tintColor = null)
    {
        if (targetMonster != null)
        {
            if (spriteRenderer == null) spriteRenderer = targetMonster.GetComponentInChildren<SpriteRenderer>();
            if (monsterMovement == null) monsterMovement = targetMonster.GetComponent<WalkAndAttackMonster>();
        }

        if (spriteRenderer == null) return;

        // 최초 1회만 본래 색상 보존
        if (!hasOriginalColor)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }

        // 중복 피격 시 이전 코루틴 취소 후 지속 시간 갱신
        if (debuffCoroutine != null)
        {
            StopCoroutine(debuffCoroutine);
        }

        Color targetColor = tintColor ?? new Color(0.4f, 0.7f, 1f, 1f);
        debuffCoroutine = StartCoroutine(SlowRoutine(slowRatio, duration, targetColor));
    }

    private IEnumerator SlowRoutine(float slowRatio, float duration, Color targetColor)
    {
        spriteRenderer.color = targetColor;

        if (monsterMovement != null)
        {
            if (originalSpeed <= 0f)
            {
                originalSpeed = monsterMovement.moveSpeed;
            }
            monsterMovement.moveSpeed = originalSpeed * slowRatio;
        }

        yield return new WaitForSeconds(duration);

        RestoreAndDestroy();
    }

    private void RestoreAndDestroy()
    {
        // 1. 색상 원상 복구
        if (spriteRenderer != null && hasOriginalColor)
        {
            spriteRenderer.color = originalColor;
        }

        // 2. 이동 속도 복구
        if (monsterMovement != null && originalSpeed > 0f)
        {
            monsterMovement.moveSpeed = originalSpeed;
        }

        debuffCoroutine = null;
        hasOriginalColor = false;
        Destroy(this);
    }

    private void OnDisable()
    {
        // 도중에 오브젝트 풀링 반환 또는 비활성화 시 안전 복원
        if (spriteRenderer != null && hasOriginalColor)
        {
            spriteRenderer.color = originalColor;
        }
    }
}